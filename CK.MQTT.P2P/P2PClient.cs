using CK.Core;
using CK.MQTT.Common.Pumps;
using CK.MQTT.Pumps;
using CK.MQTT.Server;
using CK.MQTT.Stores;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.P2P
{
    class P2PClient : MqttClientImpl
    {
        protected internal P2PClient(
            ProtocolConfiguration pConfig,
            MqttClientConfiguration config,
            Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            : base( pConfig, config, messageHandler )
        {
        }

        public async Task AcceptClient( IActivityMonitor? m, IMqttChannelListener channelFactory, CancellationToken cancellationToken )
        {
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
                OutputProcessor outputProcessor;
                // Enable keepalive only if we need it.
                if( Config.KeepAliveSeconds == 0 )
                {
                    outputProcessor = new OutputProcessor( ProtocolConfig, output, channel.DuplexPipe.Output, store );
                }
                else
                {
                    // If keepalive is enabled, we add it's handler to the middlewares.
                    OutputProcessorWithKeepAlive withKeepAlive = new( ProtocolConfig, Config, output, channel.DuplexPipe.Output, store );
                    outputProcessor = withKeepAlive;
                    builder.UseMiddleware( withKeepAlive );
                }
                // When receiving the ConnAck, this reflex will replace the reflex with this property.
                connectAckReflex.Reflex = builder.Build( ( a, b, c, d, e, f ) => SelfDisconnectAsync( DisconnectedReason.ProtocolError ) );

                await channel.StartAsync( m ); // Will create the connection to server.
                output.StartPumping( outputProcessor ); // Start processing incoming messages.

                OutgoingConnect outgoingConnect = new( ProtocolConfig, Config, credentials, lastWill );
                // Write timeout.
                CancellationTokenSource cts = Config.CancellationTokenSourceFactory.Create( Config.WaitTimeoutMilliseconds );
                // Send the packet.
                IOutgoingPacket.WriteResult writeConnectResult = await outgoingConnect.WriteAsync(
                    ProtocolConfig.ProtocolLevel, channel.DuplexPipe.Output, cts.Token
                );
                if( writeConnectResult != IOutgoingPacket.WriteResult.Written )
                {
                    await Pumps.CloseAsync();
                    return new ConnectResult( ConnectError.Timeout );
                }
                Task timeout = Config.DelayHandler.Delay( Config.WaitTimeoutMilliseconds, cancellationToken );
                Task<ConnectResult> connectedTask = connectAckReflex.Task;
                await Task.WhenAny( connectedTask, timeout ); // TODO: should I rewrite this to avoid the background unawaited task.delay ?
                                                              // This following code wouldn't be better with a sort of ... switch/pattern matching ?

                async ValueTask<ConnectResult> Exit( LogLevel logLevel, ConnectError connectError, string? log = null, Exception? exception = null )
                {
                    m?.Log( logLevel, log, exception );
                    await Pumps!.CloseAsync();
                    return new ConnectResult( connectError );
                }

                if( timeout.IsCanceled )
                    return await Exit( LogLevel.Trace, ConnectError.Connection_Cancelled, "Connection was canceled." );
                if( connectedTask.Exception is not null )
                    return await Exit( LogLevel.Fatal, ConnectError.InternalException, exception: connectedTask.Exception );
                if( Pumps.IsClosed )
                    return await Exit( LogLevel.Trace, ConnectError.RemoteDisconnected, "Remote disconnected." );
                if( !connectedTask.IsCompleted )
                    return await Exit( LogLevel.Trace, ConnectError.Timeout, "Timeout while waiting for server response." );
                ConnectResult res = await connectedTask;
                if( res.ConnectError != ConnectError.Ok )
                    return await Exit( LogLevel.Trace, res.ConnectError, "Connect code is not ok." );
                bool askedCleanSession = credentials?.CleanSession ?? true;
                if( askedCleanSession && res.SessionState != SessionState.CleanSession )
                    return await Exit( LogLevel.Warn, ConnectError.ProtocolError_SessionNotFlushed, "Session was not flushed while we asked for it." );
                if( res.SessionState == SessionState.CleanSession )
                {
                    ValueTask task = packetIdStore.ResetAsync();
                    await store.ResetAsync();
                    await task;
                }
                else
                {
                    throw new NotImplementedException();
                }
                return res;
            }
            catch( Exception e )
            {
                m?.Error( "Error while connecting, closing client.", e );
                // We may throw before the creation of the duplex pump.
                if( Pumps is not null ) await Pumps.CloseAsync(); ;
                return new ConnectResult( ConnectError.InternalException );
            }
        }
    }
}
