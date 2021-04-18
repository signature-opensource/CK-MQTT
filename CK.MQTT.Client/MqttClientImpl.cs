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
    class MqttClientImpl : IMqttClient
    {
        /// <summary>
        /// Allow to atomically get/set multiple fields.
        /// </summary>
        class ClientState : IState
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
            public readonly OutputPump OutputPump;

            public Task CloseAsync()
            {
                Channel.Close( _config.InputLogger );
                return Task.CompletedTask;
            }
        }


        DuplexPump<ClientState>? _duplex;

        readonly MqttClientConfiguration _config;
        readonly ProtocolConfiguration _pConfig;
        Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> _messageHandler;

        /// <summary>
        /// Instantiate the <see cref="MqttClientImpl"/> with the given configuration.
        /// </summary>
        /// <param name="config">The configuration to use.</param>
        /// <param name="messageHandler">The delegate that will handle incoming messages. <see cref="MessageHandlerDelegate"/> docs for more info.</param>
        internal MqttClientImpl( ProtocolConfiguration pConfig, MqttClientConfiguration config, Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
        {
            (_config, _messageHandler) = (config, messageHandler);
            if( config.WaitTimeoutMilliseconds > config.KeepAliveSeconds * 1000 && config.KeepAliveSeconds != 0 )
            {
                throw new ArgumentException( "Wait timeout should be smaller than the keep alive." );
            }
            _pConfig = pConfig;
        }


        /// <summary>
        /// This method is required so the delegate used in the Reflex doesn't change.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        ValueTask OnMessage( IActivityMonitor m, string topic, PipeReader pipeReader, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken )
            => _messageHandler( m, topic, pipeReader, payloadLength, qos, retain, cancellationToken );

        /// <inheritdoc/>
        public async Task<ConnectResult> ConnectAsync( IActivityMonitor? m, MqttClientCredentials? credentials = null, OutgoingLastWill? lastWill = null, CancellationToken cancellationToken = default )
        {
            if( _duplex?.IsRunning ?? false ) throw new InvalidOperationException( "This client is already connected." );
            using( m?.OpenTrace( "Connecting..." ) )
            {
                try
                {
                    (IOutgoingPacketStore store, IIncomingPacketStore packetIdStore) = await _config.StoreFactory.CreateAsync( m, _pConfig, _config, _config.ConnectionString, credentials?.CleanSession ?? true );
                    IMqttChannel channel = await _config.ChannelFactory.CreateAsync( m, _config.ConnectionString );
                    ConnectAckReflex connectAckReflex = new();
                    Task<ConnectResult> connectedTask = connectAckReflex.Task;
                    var output = new OutputPump( SelfDisconnect, _config );
                    OutputProcessor outputProcessor;
                    var input = new InputPump( SelfDisconnect, _config, channel.DuplexPipe.Input, connectAckReflex.ProcessIncomingPacket );
                    _duplex = new DuplexPump<ClientState>( new ClientState( _config, output, channel, packetIdStore, store ), input, output );

                    ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder()
                        .UseMiddleware( new PublishReflex( _config, packetIdStore, OnMessage, output ) )
                        .UseMiddleware( new PublishLifecycleReflex( packetIdStore, store, output ) )
                        .UseMiddleware( new SubackReflex( store ) )
                        .UseMiddleware( new UnsubackReflex( store ) );
                    if( _config.KeepAliveSeconds == 0 )
                    {
                        outputProcessor = new OutputProcessor( _pConfig, output, channel.DuplexPipe.Output, store );
                    }
                    else
                    {
                        OutputProcessorWithKeepAlive withKeepAlive = new( _pConfig, _config, output, channel.DuplexPipe.Output, store );
                        outputProcessor = withKeepAlive;
                        _ = builder.UseMiddleware( withKeepAlive );
                    }
                    output.StartPumping( outputProcessor );
                    connectAckReflex.Reflex = builder.Build( ( a, b, c, d, e, f ) => SelfDisconnect( DisconnectedReason.ProtocolError ) );
                    OutgoingConnect outgoingConnect = new( _pConfig, _config, credentials, lastWill );
                    CancellationTokenSource cts = new( _config.WaitTimeoutMilliseconds );
                    IOutgoingPacket.WriteResult writeConnectResult = await outgoingConnect.WriteAsync( _pConfig.ProtocolLevel, channel.DuplexPipe.Output, cts.Token );
                    if( writeConnectResult != IOutgoingPacket.WriteResult.Written )
                    {
                        await _duplex.CloseAsync();
                        return new ConnectResult( ConnectError.Timeout );
                    }
                    Task timeout = _config.DelayHandler.Delay( _config.WaitTimeoutMilliseconds, cancellationToken );
                    _ = await Task.WhenAny( connectedTask, timeout );
                    // This following code wouldn't be better with a sort of ... switch/pattern matching ?
                    if( timeout.IsCanceled )
                    {
                        m?.Trace( "Connection was canceled." );
                        await _duplex.CloseAsync();
                        return new ConnectResult( ConnectError.Connection_Cancelled );
                    }

                    if( connectedTask.Exception is not null )
                    {
                        m?.Fatal( connectedTask.Exception );
                        await _duplex.CloseAsync();
                        return new ConnectResult( ConnectError.InternalException );
                    }
                    if( _duplex.IsClosed )
                    {
                        await _duplex.CloseAsync();
                        return new ConnectResult( ConnectError.RemoteDisconnected );
                    }
                    if( !connectedTask.IsCompleted )
                    {
                        await _duplex.CloseAsync();
                        return new ConnectResult( ConnectError.Timeout );
                    }
                    ConnectResult res = await connectedTask;
                    if( res.ConnectError != ConnectError.Ok )
                    {
                        await _duplex.CloseAsync();
                        return new ConnectResult( res.ConnectError );
                    }
                    bool askedCleanSession = credentials?.CleanSession ?? true;
                    if( askedCleanSession && res.SessionState != SessionState.CleanSession )
                    {
                        await _duplex.CloseAsync();
                        return new ConnectResult( ConnectError.ProtocolError_SessionNotFlushed );
                    }
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
                    await _duplex.CloseAsync();
                    return new ConnectResult( ConnectError.InternalException );
                }
            }
        }

        public bool IsConnected => _duplex?.IsRunning ?? false;

        public Disconnected? DisconnectedHandler { get; set; }

        internal async ValueTask SelfDisconnect( DisconnectedReason disconnectedReason )
        {
            Debug.Assert( _duplex != null );
            await _duplex.CloseAsync();
            DisconnectedHandler?.Invoke( disconnectedReason );
        }

        /// <inheritdoc/>
        public void SetMessageHandler( Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => _messageHandler = messageHandler;

        public ValueTask<Task<T?>> SendPacket<T>( IActivityMonitor? m, IOutgoingPacket outgoingPacket ) where T : class
        {
            if( !IsConnected ) throw new InvalidOperationException( "Client is Disconnected." );
            DuplexPump<ClientState>? duplex = _duplex;
            if( duplex is null ) throw new NullReferenceException();
            return SenderHelper.SendPacket<T>( m, duplex.State.Store, duplex.State.OutputPump, outgoingPacket );
        }

        /// <summary>
        /// Called by the external world to explicitly close the connection to the remote.
        /// </summary>
        /// <returns>True if this call actually closed the connection, false if the connection has already been closed by a concurrent decision.</returns>
        public async Task<bool> DisconnectAsync( IActivityMonitor? m, bool clearSession, bool cancelAckTasks, CancellationToken cancellationToken )
        {
            DuplexPump<ClientState>? duplex = _duplex;
            if( clearSession && !cancelAckTasks ) throw new ArgumentException( "When the session is cleared, the ACK tasks must be canceled too." );
            if( duplex is null ) return false;
            if( duplex.IsRunning ) return false;
            if( cancelAckTasks ) duplex.State.Store.CancelAllAckTask( m );
            await duplex.StopWork();
            if( duplex.IsClosed ) return false;
            // Because we stopped the pumps, their states cannot change concurrently.

            //TODO: the return type may be not enough there, if the cancellation token was triggered, we may not know the final
            await OutgoingDisconnect.Instance.WriteAsync( _pConfig.ProtocolLevel, duplex.State.Channel.DuplexPipe.Output, cancellationToken );
            await duplex.CloseAsync();
            duplex = null;
            return true;
        }
    }
}
