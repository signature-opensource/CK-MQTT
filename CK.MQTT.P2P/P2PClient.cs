using CK.Core;
using CK.MQTT.Common.Pumps;
using CK.MQTT.Pumps;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.P2P
{
    public class P2PClient : MqttClientImpl
    {
        internal P2PClient(
            MqttClientConfiguration config,
            Func<IActivityMonitor, string, PipeReader, uint, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            : base( ProtocolConfiguration.Mqtt5, config, messageHandler )
        {
        }

        public async Task<ConnectError> AcceptClientAsync( IActivityMonitor? m, IMqttChannelListener channelFactory, CancellationToken cancellationToken )
        {
            if( Config.KeepAliveSeconds != 0 ) throw new NotSupportedException( "Server KeepAlive is not yet supported." );
            if( Pumps?.IsRunning ?? false ) throw new InvalidOperationException( "This client is already connected." );
            try
            {
                IMqttChannel channel;
                string clientAddress;
                using( m?.OpenTrace( "Awaiting remote connection." ) )
                {
                    (channel, clientAddress) = await channelFactory.AcceptIncomingConnection( cancellationToken );
                }

                ConnectReflex connectReflex = new( ProtocolConfig, Config );
                // Creating pumps. Need to be started.
                InputPump inputPump = new( SelfDisconnectAsync, Config, channel.DuplexPipe.Input, connectReflex.HandleRequestAsync );

                await connectReflex.ConnectHandledTask;
                ProtocolConfig = new ProtocolConfiguration(
                    ProtocolConfig.SecurePort,
                    ProtocolConfig.NonSecurePort,
                    connectReflex.ProtocolLevel,
                    ProtocolConfig.SingleLevelTopicWildcard,
                    ProtocolConfig.MultiLevelTopicWildcard,
                    ProtocolConfig.ProtocolName,
                    ProtocolConfig.MaximumPacketSize
                );
                OutputPump output = new( connectReflex.OutStore, SelfDisconnectAsync, Config );

                // This reflex handle the connection packet.
                // It will replace itself with the regular packet processing.

                Pumps = new DuplexPump<ClientState>(
                    new ClientState( Config, output, channel, connectReflex.InStore, connectReflex.OutStore ),
                    output,
                    inputPump
                );

                // Middleware that will processes the requests.
                ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder()
                    .UseMiddleware( new PublishReflex( Config, connectReflex.InStore, OnMessageAsync, output ) )
                    .UseMiddleware( new PublishLifecycleReflex( connectReflex.InStore, connectReflex.OutStore, output ) )
                    .UseMiddleware( new SubackReflex( connectReflex.OutStore ) )
                    .UseMiddleware( new UnsubackReflex( connectReflex.OutStore ) );
                OutputProcessor outputProcessor = new( ProtocolConfig, output, channel.DuplexPipe.Output, connectReflex.OutStore, connectReflex.InStore );
                // Enable keepalive only if we need it.

                // When receiving the ConnAck, this reflex will replace the reflex with this property.
                Reflex reflex = builder.Build( async ( a, b, c, d, e, f ) =>
                {
                    await SelfDisconnectAsync( DisconnectedReason.ProtocolError );
                    return OperationStatus.Done;
                } );
                connectReflex.EngageNextReflex( reflex );
                await channel.StartAsync( m ); // Will create the connection to server.
                output.StartPumping( outputProcessor ); // Start processing incoming messages.


                if( Pumps.IsClosed )
                {
                    m?.Trace( "Remote disconnected." );
                    await Pumps!.CloseAsync();
                    return ConnectError.RemoteDisconnected;
                }

                bool hasExistingSession = connectReflex.OutStore.IsRevivedSession || connectReflex.InStore.IsRevivedSession;
                await output.QueueMessageAndWaitUntilSentAsync( m, new ConnectAckPacket( hasExistingSession, ConnectReturnCode.Accepted ) );

                return ConnectError.Ok;
            }
            catch( Exception e )
            {
                m?.Error( "Error while connecting, closing client.", e );
                // We may throw before the creation of the duplex pump.
                if( Pumps is not null ) await Pumps.CloseAsync(); ;
                return ConnectError.InternalException;
            }
        }
    }
}
