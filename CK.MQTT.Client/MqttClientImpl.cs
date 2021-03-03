using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class MqttClientImpl : Pumppeteer<MqttClientImpl.ClientState>, IMqttClient
    {
        /// <summary>
        /// Allow to atomically get/set multiple fields.
        /// </summary>
        internal class ClientState : StateHolder
        {
            public ClientState( InputPump input, OutputPump output, IMqttChannel channel, IIncomingPacketStore packetIdStore, IOutgoingPacketStore store ) : base( input, output )
            {
                Channel = channel;
                PacketIdStore = packetIdStore;
                Store = store;
            }
            public readonly IMqttChannel Channel;
            public readonly IIncomingPacketStore PacketIdStore;
            public readonly IOutgoingPacketStore Store;
        }



        readonly ProtocolConfiguration _pConfig;
        readonly MqttClientConfiguration _config;
        Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> _messageHandler;

        /// <summary>
        /// Instantiate the <see cref="MqttClientImpl"/> with the given configuration.
        /// </summary>
        /// <param name="config">The configuration to use.</param>
        /// <param name="messageHandler">The delegate that will handle incoming messages. <see cref="MessageHandlerDelegate"/> docs for more info.</param>
        internal MqttClientImpl( ProtocolConfiguration protocolConfig, MqttClientConfiguration config, Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            : base( config )
        {
            (_pConfig, _config, _messageHandler) = (protocolConfig, config, messageHandler);
            if( config.WaitTimeoutMilliseconds > config.KeepAliveSeconds )
            {
                throw new ArgumentException( "Wait timeout should be smaller than the keep alive." );
            }

        }


        /// <summary>
        /// This method is required so the delegate used in the Reflex doesn't change.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        ValueTask OnMessage( IActivityMonitor m, string topic, PipeReader pipeReader, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken )
            => _messageHandler( m, topic, pipeReader, payloadLength, qos, retain, cancellationToken );

        /// <inheritdoc/>
        public async Task<ConnectResult> ConnectAsync( IActivityMonitor? m, MqttClientCredentials? credentials = null, OutgoingLastWill? lastWill = null )
        {
            if( IsConnected ) throw new InvalidOperationException( "This client is already connected." );
            using( m?.OpenTrace( "Connecting..." ) )
            {
                try
                {
                    (IOutgoingPacketStore store, IIncomingPacketStore packetIdStore) = await _config.StoreFactory.CreateAsync( m, _pConfig, _config, _config.ConnectionString, credentials?.CleanSession ?? true );
                    IMqttChannel channel = await _config.ChannelFactory.CreateAsync( m, _config.ConnectionString );
                    ConnectAckReflex connectAckReflex = new ConnectAckReflex();
                    Task<ConnectResult> connectedTask = connectAckReflex.Task;
                    OutputProcessorWithKeepAlive outputProcessorWithKeepAlive = new OutputProcessorWithKeepAlive();
                    var input = new InputPump( this, channel.DuplexPipe.Input, connectAckReflex.ProcessIncomingPacket );
                    var output = new OutputPump( this, _pConfig,  );
                    OpenPumps( m, new ClientState( input, output, channel, packetIdStore, store ) );
                    PingRespReflex pingRes = new PingRespReflex();
                    connectAckReflex.Reflex = new ReflexMiddlewareBuilder()
                        .UseMiddleware( new PublishReflex( _config, packetIdStore, OnMessage, output ) )
                        .UseMiddleware( new PublishLifecycleReflex( packetIdStore, store, output ) )
                        .UseMiddleware( new SubackReflex( store ) )
                        .UseMiddleware( new UnsubackReflex( store ) )
                        .UseMiddleware( pingRes )
                        .Build( InvalidPacket );

                    await output.SendMessageAsync( m, new OutgoingConnect( _pConfig, _config, credentials, lastWill ) );
                    output.SetOutputProcessor( new MainOutputProcessor( _config, store, pingRes ).OutputProcessor );
                    // TODO: there is 2 output processor to not have the ping on the connect.
                    // fix this.
                    Task timeout = _config.DelayHandler.Delay( _config.WaitTimeoutMilliseconds, CloseToken );
                    _ = await Task.WhenAny( connectedTask, timeout );
                    // This following code wouldn't be better with a sort of ... switch/pattern matching ?
                    if( connectedTask.Exception is not null )
                    {
                        m?.Fatal( connectedTask.Exception );
                        _ = await CloseAsync( DisconnectedReason.None );
                        return new ConnectResult( ConnectError.InternalException );
                    }
                    if( CloseToken.IsCancellationRequested )
                    {
                        _ = await CloseAsync( DisconnectedReason.None );
                        return new ConnectResult( ConnectError.RemoteDisconnected );
                    }
                    if( !connectedTask.IsCompleted )
                    {
                        _ = await CloseAsync( DisconnectedReason.None );
                        return new ConnectResult( ConnectError.Timeout );
                    }
                    ConnectResult res = await connectedTask;
                    if( res.ConnectError != ConnectError.Ok )
                    {
                        _ = await CloseAsync( DisconnectedReason.None );
                        return new ConnectResult( res.ConnectError );
                    }
                    bool askedCleanSession = credentials?.CleanSession ?? true;
                    if( askedCleanSession && res.SessionState != SessionState.CleanSession )
                    {
                        _ = await CloseAsync( DisconnectedReason.None );
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
                    _ = await CloseAsync( DisconnectedReason.None );
                    return new ConnectResult( ConnectError.InternalException );
                }
            }
        }

        async ValueTask InvalidPacket( IInputLogger? m, InputPump sender, byte header, int packetSize, PipeReader reader, CancellationToken cancellationToken )
        {
            _ = await CloseAsync( DisconnectedReason.ProtocolError );
            throw new ProtocolViolationException();
        }

        /// <inheritdoc/>
        public void SetMessageHandler( Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
        {
            _messageHandler = messageHandler;
        }

        protected override async ValueTask OnClosingAsync( DisconnectedReason reason )
        {
            if( reason == DisconnectedReason.UserDisconnected )
            {
                await State!.OutputPump.SendMessageAsync( null, OutgoingDisconnect.Instance ); // TODO: We need a logger here.
            }
        }

        protected override ValueTask OnClosed( DisconnectedReason reason )
        {
            State!.Channel.Close( _config.InputLogger );
            return base.OnClosed( reason );
        }

        public ValueTask<Task<T?>> SendPacket<T>( IActivityMonitor? m, IOutgoingPacketWithId outgoingPacket ) where T : class
        {
            ClientState? state = State;
            if( !IsConnected ) throw new InvalidOperationException( "Client is Disconnected." );
            if( state is null ) throw new NullReferenceException();
            return SenderHelper.SendPacket<T>( m, state.Store, state.OutputPump, outgoingPacket );
        }

        /// <summary>
        /// Called by the external world to explicitly close the connection to the remote.
        /// </summary>
        /// <param name="reason">The reason of the disconnection.</param>
        /// <returns>True if this call actually closed the connection, false if the connection has already been closed by a concurrent decision.</returns>
        public Task<bool> DisconnectAsync( IActivityMonitor? m, bool clearSession, bool cancelAckTasks )
        {
            ClientState? state = State;
            if( clearSession && !cancelAckTasks ) throw new ArgumentException( "When the session is cleared, the ACK tasks must be canceled too." );
            if( !IsConnected ) return Task.FromResult( false );
            if( state is null ) throw new NullReferenceException();
            if( cancelAckTasks ) state!.Store.CancelAllAckTask( m );
            return CloseAsync( DisconnectedReason.UserDisconnected );
        }
    }
}
