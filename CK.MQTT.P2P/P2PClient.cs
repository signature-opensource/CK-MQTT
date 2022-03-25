using CK.MQTT.Common.Pumps;
using CK.MQTT.Packets;
using CK.MQTT.Pumps;
using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.P2P
{
    public class P2PClient : MqttClientImpl
    {
        readonly IMqtt5ServerClientSink _sink;

        internal P2PClient( IMqtt5ServerClientSink sink, P2PMqttConfiguration config )
            : base( sink, config )
        {
            _sink = sink;
            P2PConfig = config;
        }

        public P2PMqttConfiguration P2PConfig { get; }

        public async Task<ConnectError> AcceptClientAsync( IMqttChannelListener channelFactory, CancellationToken cancellationToken )
        {
            if( Config.KeepAliveSeconds != 0 ) throw new NotSupportedException( "Server KeepAlive is not yet supported." );
            if( Pumps?.IsRunning ?? false ) throw new InvalidOperationException( "This client is already connected." );
            try
            {
                IMqttChannel channel;
                string clientAddress;
                (channel, clientAddress) = await channelFactory.AcceptIncomingConnection( cancellationToken );

                ConnectReflex connectReflex = new( _sink, P2PConfig.ProtocolConfiguration, P2PConfig );
                // Creating pumps. Need to be started.
                InputPump inputPump = new( _sink, SelfDisconnectAsync, Config, channel.DuplexPipe.Input, connectReflex.HandleRequestAsync );

                await connectReflex.ConnectHandledTask;
                
                OutputPump output = new( _sink, connectReflex.OutStore, SelfDisconnectAsync, Config );

                // This reflex handle the connection packet.
                // It will replace itself with the regular packet processing.

                Pumps = new DuplexPump<ClientState>(
                    new ClientState( output, channel ),
                    output,
                    inputPump
                );

                // Middleware that will processes the requests.
                ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder()
                    .UseMiddleware( new PublishReflex( Config, connectReflex.InStore, OnMessageAsync, output ) )
                    .UseMiddleware( new PublishLifecycleReflex( connectReflex.InStore, connectReflex.OutStore, output ) )
                    .UseMiddleware( new SubackReflex( connectReflex.OutStore ) )
                    .UseMiddleware( new UnsubackReflex( connectReflex.OutStore ) );
                OutputProcessor outputProcessor = new( P2PConfig.ProtocolConfiguration, output, channel.DuplexPipe.Output, connectReflex.OutStore );
                // Enable keepalive only if we need it.

                // When receiving the ConnAck, this reflex will replace the reflex with this property.
                Reflex reflex = builder.Build( async ( a, b, c, d, e, f ) =>
                {
                    await SelfDisconnectAsync( DisconnectReason.ProtocolError );
                    return OperationStatus.Done;
                } );
                connectReflex.EngageNextReflex( reflex );
                await channel.StartAsync(); // Will create the connection to server.
                output.StartPumping( outputProcessor ); // Start processing incoming messages.


                if( Pumps.IsClosed )
                {
                    await Pumps!.CloseAsync();
                    return ConnectError.RemoteDisconnected;
                }

                bool hasExistingSession = connectReflex.OutStore.IsRevivedSession || connectReflex.InStore.IsRevivedSession;
                await output.QueueMessageAndWaitUntilSentAsync( new ConnectAckPacket( hasExistingSession, ConnectReturnCode.Accepted ) );

                return ConnectError.None;
            }
            catch( Exception )
            {
                // We may throw before the creation of the duplex pump.
                if( Pumps is not null ) await Pumps.CloseAsync(); ;
                return ConnectError.InternalException;
            }
        }
    }
}
