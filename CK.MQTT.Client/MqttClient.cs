using CK.Core;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    internal class MqttClient : Pumppeteer<MqttClient.ClientState>, IMqttClient
    {
        /// <summary>
        /// Allow to atomically get/set multiple fields.
        /// </summary>
        internal class ClientState : StateHolder
        {
            public ClientState( InputPump input, OutputPump output, IMqttChannel channel, IPacketIdStore packetIdStore, IPacketStore store ) : base( input, output )
            {
                Channel = channel;
                PacketIdStore = packetIdStore;
                Store = store;
            }
            public readonly IMqttChannel Channel;
            public readonly IPacketIdStore PacketIdStore;
            public readonly IPacketStore Store;
        }

        /// <summary>
        /// Factory to use to create a MQTT Client.
        /// </summary>
        public static MqttClientFactory Factory { get; } = new MqttClientFactory();

        readonly ProtocolConfiguration _pConfig;
        readonly MqttConfiguration _config;
        Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> _messageHandler;

        /// <summary>
        /// Instantiate the <see cref="MqttClient"/> with the given configuration.
        /// </summary>
        /// <param name="config">The config to use.</param>
        /// <param name="messageHandler">The delegate that will handle incoming messages. <see cref="MessageHandlerDelegate"/> docs for more info.</param>
        internal MqttClient( ProtocolConfiguration protocolConfig, MqttConfiguration config, Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            : base( config )
            => (_pConfig, _config, _messageHandler) = (protocolConfig, config, messageHandler);


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
                    (IPacketStore store, IPacketIdStore packetIdStore) = await _config.StoreFactory.CreateAsync( m, _pConfig, _config, _config.ConnectionString, credentials?.CleanSession ?? true );
                    IMqttChannel channel = await _config.ChannelFactory.CreateAsync( m, _config.ConnectionString );
                    ConnectAckReflex connectAckReflex = new ConnectAckReflex();
                    Task<ConnectResult> connectedTask = connectAckReflex.Task;
                    var input = new InputPump( this, channel.DuplexPipe.Input, connectAckReflex.ProcessIncomingPacket );
                    var output = new OutputPump( this, _pConfig, DumbOutputProcessor.OutputProcessor, channel.DuplexPipe.Output, store );
                    OpenPumps( m, new ClientState( input, output, channel, packetIdStore, store ) );
                    PingRespReflex pingRes = new PingRespReflex();
                    connectAckReflex.Reflex = new ReflexMiddlewareBuilder()
                        .UseMiddleware( new PublishReflex( _config, packetIdStore, OnMessage, output ) )
                        .UseMiddleware( new PublishLifecycleReflex( packetIdStore, store, output ) )
                        .UseMiddleware( new SubackReflex( store ) )
                        .UseMiddleware( new UnsubackReflex( store ) )
                        .UseMiddleware( pingRes )
                        .Build( InvalidPacket );

                    await output.SendMessageWithoutPacketIdAsync( new OutgoingConnect( _pConfig, _config, credentials, lastWill ) );
                    output.SetOutputProcessor( new MainOutputProcessor( _config, store, pingRes ).OutputProcessor );
                    Task timeout = _config.DelayHandler.Delay( _config.WaitTimeoutMilliseconds, CloseToken );
                    await Task.WhenAny( connectedTask, timeout );
                    if( connectedTask.Exception is not null ) throw connectedTask.Exception.InnerException ?? connectedTask.Exception;
                    if( CloseToken.IsCancellationRequested )
                    {
                        await AutoDisconnectAsync();
                        return new ConnectResult( ConnectError.RemoteDisconnected );
                    }
                    if( !connectedTask.IsCompleted )
                    {
                        await AutoDisconnectAsync();
                        return new ConnectResult( ConnectError.Timeout );
                    }
                    ConnectResult res = await connectedTask;
                    bool askedCleanSession = credentials?.CleanSession ?? true;
                    if( askedCleanSession && res.SessionState != SessionState.CleanSession )
                    {
                        await AutoDisconnectAsync();
                        throw new ProtocolViolationException( "We asked for a clean session but broker's CONNACK had SessionPresent bit set." );
                    }
                    if( res.SessionState == SessionState.CleanSession )
                    {
                        ValueTask task = packetIdStore.ResetAsync();
                        await store.ResetAsync();
                        await task;
                    }
                    else
                    {
                        await SendAllStoredMessages( m, store, output );
                    }
                    return res;
                }
                catch( Exception e )
                {
                    m?.Error( "Error while connecting, closing client.", e );
                    await CloseAsync( DisconnectedReason.None );
                    throw;
                }
            }
        }

        async static Task SendAllStoredMessages( IActivityMonitor? m, IPacketStore store, OutputPump output )
        {
            IAsyncEnumerable<IOutgoingPacketWithId> msgs = await store.GetAllMessagesAsync( m );
            await foreach( IOutgoingPacketWithId msg in msgs )
            {
                await output.SendMessageWithPacketIdAsync( msg );
            }
        }


        async ValueTask InvalidPacket( IInputLogger? m, InputPump sender, byte header, int packetSize, PipeReader reader, CancellationToken cancellationToken )
        {
            await AutoDisconnectAsync( DisconnectedReason.ProtocolError );
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
                await State!.OutputPump.SendMessageWithoutPacketIdAsync( OutgoingDisconnect.Instance );
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
            if( cancelAckTasks ) state!.Store.IdStore.CancelAllAcks( m );
            return CloseAsync( DisconnectedReason.UserDisconnected );
        }

        /// <summary>
        /// This protected method can be called by this specialized "pumppeteer" to explicitly close the connection.
        /// </summary>
        /// <param name="reason">The reason of the disconnection.</param>
        /// <returns>True if this call actually closed the connection, false if the connection has already been closed by a concurrent decision.</returns>
        protected Task<bool> AutoDisconnectAsync( DisconnectedReason reason = DisconnectedReason.None ) => CloseAsync( reason );
    }
}
