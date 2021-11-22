using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.Common.Pumps;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class MqttClientImpl : IMqttClient
    {
        /// <summary>
        /// Allow to atomically get/set multiple fields.
        /// </summary>
        protected class ClientState : IState
        {
            public ClientState( MqttConfigurationBase config, OutputPump outputPump, IMqttChannel channel, IIncomingPacketStore packetIdStore, IOutgoingPacketStore store )
            {
                _config = config;
                OutputPump = outputPump;
                Channel = channel;
                PacketIdStore = packetIdStore;
                Store = store;
            }

            public readonly IMqttChannel Channel;
            public readonly IIncomingPacketStore PacketIdStore;
            public readonly IOutgoingPacketStore Store;
            readonly MqttConfigurationBase _config;
            public OutputPump OutputPump { get; }

            public Task CloseAsync()
            {
                Channel.Close( _config.InputLogger );
                Channel.Dispose();
                return Task.CompletedTask;
            }
        }


        protected DuplexPump<ClientState>? Pumps { get; set; }

        protected MqttClientConfiguration Config { get; }
        protected ProtocolConfiguration ProtocolConfig { get; }

        Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> _messageHandler;

        /// <summary>
        /// Instantiate the <see cref="MqttClientImpl"/> with the given configuration.
        /// </summary>
        /// <param name="config">The configuration to use.</param>
        /// <param name="messageHandler">The delegate that will handle incoming messages. <see cref="MessageHandlerDelegate"/> docs for more info.</param>
        internal protected MqttClientImpl( ProtocolConfiguration pConfig, MqttClientConfiguration config, Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
        {
            (Config, _messageHandler) = (config, messageHandler);
            if( config.WaitTimeoutMilliseconds > config.KeepAliveSeconds * 1000 && config.KeepAliveSeconds != 0 )
            {
                throw new ArgumentException( "Wait timeout should be smaller than the keep alive." );
            }
            ProtocolConfig = pConfig;

        }

        /// <summary>
        /// This method is required so the delegate used in the Reflex doesn't change.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        protected ValueTask OnMessageAsync( IActivityMonitor m, string topic, PipeReader pipeReader, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken )
            => _messageHandler( m, topic, pipeReader, payloadLength, qos, retain, cancellationToken );

        /// <inheritdoc/>
        public async Task<ConnectResult> ConnectAsync( IActivityMonitor? m, MqttClientCredentials? credentials = null, OutgoingLastWill? lastWill = null, CancellationToken cancellationToken = default )
        {
            if( lastWill != null )
            {
                MqttBinaryWriter.ThrowIfInvalidMQTTString( lastWill.Topic );
            }
            if( credentials != null )
            {
                MqttBinaryWriter.ThrowIfInvalidMQTTString( credentials.ClientId );
                if( credentials.Password != null )
                {
                    MqttBinaryWriter.ThrowIfInvalidMQTTString( credentials.Password );
                }
            }

            if( Pumps?.IsRunning ?? false ) throw new InvalidOperationException( "This client is already connected." );
            using( m?.OpenTrace( "Connecting..." ) )
            {
                try
                {
                    // Creating store from credentials (this allow to have one state per endpoint).
                    (IOutgoingPacketStore store, IIncomingPacketStore packetIdStore) = await Config.StoreFactory.CreateAsync(
                        m, ProtocolConfig, Config, Config.ConnectionString, credentials?.CleanSession ?? true
                    );

                    // Creating channel. The channel is not yet connected, but other object need a reference to it.
                    IMqttChannel channel = await Config.ChannelFactory.CreateAsync( m, Config.ConnectionString );

                    // This reflex handle the connection packet.
                    // It will replace itself with the regular packet processing.
                    ConnectAckReflex connectAckReflex = new();

                    // Creating pumps. Need to be started.
                    OutputPump output = new( store, SelfDisconnectAsync, Config );


                    // Middleware that will processes the requests.
                    ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder()
                        .UseMiddleware( new PublishReflex( Config, packetIdStore, OnMessageAsync, output ) )
                        .UseMiddleware( new PublishLifecycleReflex( packetIdStore, store, output ) )
                        .UseMiddleware( new SubackReflex( store ) )
                        .UseMiddleware( new UnsubackReflex( store ) );


                    await channel.StartAsync( m ); // Will create the connection to server.

                    OutputProcessor outputProcessor;
                    // Enable keepalive only if we need it.
                    if( Config.KeepAliveSeconds == 0 )
                    {
                        outputProcessor = new OutputProcessor( ProtocolConfig, output, channel.DuplexPipe.Output, store );
                    }
                    else
                    {
                        // If keepalive is enabled, we add it's handler to the middlewares.
                        OutputProcessorWithKeepAlive withKeepAlive = new( ProtocolConfig, Config, output, channel.DuplexPipe.Output, store ); // Require channel started.
                        outputProcessor = withKeepAlive;
                        builder.UseMiddleware( withKeepAlive );
                    }
                    // When receiving the ConnAck, this reflex will replace the reflex with this property.
                    connectAckReflex.Reflex = builder.Build( ( a, b, c, d, e, f ) => SelfDisconnectAsync( DisconnectedReason.ProtocolError ) );

                    Pumps = new DuplexPump<ClientState>( // Require channel started.
                        new ClientState( Config, output, channel, packetIdStore, store ),
                        output,
                        new InputPump( SelfDisconnectAsync, Config, channel.DuplexPipe.Input, connectAckReflex.HandleRequestAsync )
                    );
                    output.StartPumping( outputProcessor ); // Start processing incoming messages.

                    OutgoingConnect outgoingConnect = new( ProtocolConfig, Config, credentials, lastWill );

                    IOutgoingPacket.WriteResult writeConnectResult;
                    using( CancellationTokenSource cts = Config.CancellationTokenSourceFactory.Create( Config.WaitTimeoutMilliseconds ) )
                    {
                        // Send the packet.
                        writeConnectResult = await outgoingConnect.WriteAsync( ProtocolConfig.ProtocolLevel, channel.DuplexPipe.Output, cts.Token );
                    };

                    async ValueTask<ConnectResult> Exit( LogLevel logLevel, ConnectError connectError, string? log = null, Exception? exception = null )
                    {
                        m?.Log( logLevel, log, exception );
                        await Pumps!.CloseAsync();
                        channel.Dispose();
                        return new ConnectResult( connectError );
                    }

                    if( writeConnectResult != IOutgoingPacket.WriteResult.Written )
                        return await Exit( LogLevel.Error, ConnectError.Timeout, "Timeout while writing connect packet." );
                    ConnectResult res;
                    using( CancellationTokenSource cts2 = Config.CancellationTokenSourceFactory.Create( cancellationToken, Config.WaitTimeoutMilliseconds ) )
                    {
                        cts2.Token.Register( () => connectAckReflex.TrySetCanceled( cancellationToken ) );
                        try
                        {
                            res = await connectAckReflex.Task;
                        }
                        catch( OperationCanceledException )
                        {
                            // This following code wouldn't be better with a sort of ... switch/pattern matching ?
                            if( cancellationToken.IsCancellationRequested )
                            {
                                return await Exit( LogLevel.Trace, ConnectError.Connection_Cancelled, "Given cancellation token was canceled." );
                            }
                            else
                            {
                                return await Exit( LogLevel.Error, ConnectError.Timeout, $"Server accepted transport connexion but didn't answered to MQTT connexion before the configure timeout({ Config.WaitTimeoutMilliseconds}ms)." );
                            }
                        }
                    }

                    // This following code wouldn't be better with a sort of ... switch/pattern matching ?
                    if( cancellationToken.IsCancellationRequested )
                        return await Exit( LogLevel.Trace, ConnectError.Connection_Cancelled, "Given cancellation token was canceled." );
                    if( connectAckReflex.Task.Exception is not null || connectAckReflex.Task.IsFaulted )
                        return await Exit( LogLevel.Fatal, ConnectError.InternalException, exception: connectAckReflex.Task.Exception );
                    if( Pumps.IsClosed )
                        return await Exit( LogLevel.Trace, ConnectError.RemoteDisconnected, "Remote disconnected." );

                    if( res.ConnectError != ConnectError.Ok )
                    {
                        m?.Trace( "Connect code is not ok." );
                        await Pumps!.CloseAsync();
                        channel.Dispose();
                        return res;
                    }

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
                    if( Pumps is not null )
                    {
                        await Pumps.CloseAsync();
                        Pumps.Dispose();
                    }
                    return new ConnectResult( ConnectError.InternalException );
                }
            }
        }

        public bool IsConnected => Pumps?.IsRunning ?? false;

        public Disconnected? DisconnectedHandler { get; set; }

        protected async ValueTask SelfDisconnectAsync( DisconnectedReason disconnectedReason )
        {
            Debug.Assert( Pumps != null );
            await Pumps.CloseAsync();
            DisconnectedHandler?.Invoke( disconnectedReason );
        }

        /// <inheritdoc/>
        public void SetMessageHandler( Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => _messageHandler = messageHandler;

        public ValueTask<Task<T?>> SendPacketAsync<T>( IActivityMonitor? m, IOutgoingPacket outgoingPacket ) where T : class
        {
            if( !IsConnected ) throw new InvalidOperationException( "Client is Disconnected." );
            DuplexPump<ClientState>? duplex = Pumps;
            if( duplex is null ) throw new NullReferenceException();
            return SenderHelper.SendPacketAsync<T>( m, duplex.State.Store, duplex.State.OutputPump, outgoingPacket );
        }

        /// <summary>
        /// Called by the external world to explicitly close the connection to the remote.
        /// </summary>
        /// <returns>True if this call actually closed the connection, false if the connection has already been closed by a concurrent decision.</returns>
        public async Task<bool> DisconnectAsync( IActivityMonitor? m, bool clearSession, bool cancelAckTasks, CancellationToken cancellationToken )
        {
            DuplexPump<ClientState>? duplex = Pumps;
            if( clearSession && !cancelAckTasks ) throw new ArgumentException( "When the session is cleared, the ACK tasks must be canceled too." );
            if( duplex is null ) return false;
            if( duplex.IsRunning ) return false;
            if( cancelAckTasks ) duplex.State.Store.CancelAllAckTask( m );
            await duplex.StopWorkAsync();
            if( duplex.IsClosed ) return false;
            // Because we stopped the pumps, their states cannot change concurrently.

            //TODO: the return type may be not enough there, if the cancellation token was triggered, we may not know the final
            await OutgoingDisconnect.Instance.WriteAsync( ProtocolConfig.ProtocolLevel, duplex.State.Channel.DuplexPipe.Output, cancellationToken );
            await duplex.CloseAsync();
            duplex.Dispose();
            Pumps = null;
            return true;
        }

        public void Dispose()
        {
            Pumps?.Dispose();
        }
    }
}
