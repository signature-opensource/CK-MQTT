using System;
using System.Net;
using System.Threading.Tasks;
using System.IO.Pipelines;
using System.Collections.Generic;
using CK.Core;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using CK.MQTT.Client;

namespace CK.MQTT
{
    public class MqttClient : Pumppeteer, IMqttClient
    {
        public static MqttClientFactory Factory { get; } = new MqttClientFactory();
        readonly ProtocolConfiguration _pConfig;

        //Dont change between lifecycles
        readonly MqttConfiguration _config;
        readonly IMqttChannelFactory _channelFactory;

        // Change between Connection/Disconnection.
        IMqttChannel? _channel;
        IPacketIdStore? _packetIdStore;
        PacketStore? _store;
        MessageHandlerDelegate _messageHandler;

        /// <summary>
        /// Instantiate the <see cref="MqttClient"/> with the given configuration.
        /// </summary>
        /// <param name="config">The config to use.</param>
        /// <param name="messageHandler">The delegate that will handle incoming messages. <see cref="MessageHandlerDelegate"/> docs for more info.</param>
        internal MqttClient( ProtocolConfiguration pConfig, MqttConfiguration config, MessageHandlerDelegate messageHandler )
            : base( config )
        {
            _pConfig = pConfig;
            _config = config;
            _messageHandler = messageHandler;
            _channelFactory = config.ChannelFactory;
        }

        [MemberNotNull( nameof( _packetIdStore ) )]
        [MemberNotNull( nameof( _store ) )]
        void ThrowIfNotConnected()
        {
            if( !IsConnected ) throw new InvalidOperationException( "Client is Disconnected." );
            Debug.Assert( InputPump != null );
            Debug.Assert( OutputPump != null );
            Debug.Assert( _packetIdStore != null );
            Debug.Assert( _store != null );
        }

        /// <summary>
        /// This method is required so the delegate used in the Reflex doesn't change.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        ValueTask OnMessage( string topic, PipeReader pipeReader, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken )
            => _messageHandler( topic, pipeReader, payloadLength, qos, retain, cancellationToken );

        /// <inheritdoc/>
        public async Task<ConnectResult> ConnectAsync( IActivityMonitor m, MqttClientCredentials? credentials = null, OutgoingLastWill? lastWill = null )
        {
            (_store, _packetIdStore) = await _config.StoreFactory.CreateAsync( m, _pConfig, _config, _config.ConnectionString, credentials?.CleanSession ?? true );

            _channel = await _channelFactory.CreateAsync( m, _config.ConnectionString );
            ConnectAckReflex connectAckReflex = new ConnectAckReflex();
            Task<ConnectResult> connectedTask = connectAckReflex.Task;
            var input = new InputPump( this, _channel.DuplexPipe.Input, connectAckReflex.ProcessIncomingPacket );
            var output = new OutputPump( this, _pConfig, DumbOutputProcessor.OutputProcessor, _channel.DuplexPipe.Output, _store );
            OpenPumps( input, output );
            PingRespReflex pingRes = new PingRespReflex();
            connectAckReflex.Reflex = new ReflexMiddlewareBuilder()
                .UseMiddleware( new PublishReflex( _packetIdStore, OnMessage, output ) )
                .UseMiddleware( new PublishLifecycleReflex( _packetIdStore, _store, output ) )
                .UseMiddleware( new SubackReflex( _store ) )
                .UseMiddleware( new UnsubackReflex( _store ) )
                .UseMiddleware( pingRes )
                .Build( InvalidPacket );

            await output.SendMessageAsync( new OutgoingConnect( _pConfig, _config, credentials, lastWill ) );
            output.SetOutputProcessor( new MainOutputProcessor( _config, _store, pingRes ).OutputProcessor );

            await Task.WhenAny( connectedTask, Task.Delay( _config.WaitTimeoutMilliseconds, CloseToken ) );
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
            if( res.SessionState == SessionState.CleanSession )
            {
                ValueTask task = _packetIdStore.ResetAsync();
                await _store.ResetAsync();
                await task;
            }
            else
            {
                await SendAllStoredMessages( m, _store, output );
            }
            return res;
        }

        async static Task SendAllStoredMessages( IActivityMonitor m, PacketStore store, OutputPump output )
        {
            IAsyncEnumerable<IOutgoingPacketWithId> msgs = await store.GetAllMessagesAsync( m );
            await foreach( IOutgoingPacketWithId msg in msgs )
            {
                await output.SendMessageAsync( msg );
            }
        }


        async ValueTask InvalidPacket( IInputLogger? m, InputPump sender, byte header, int packetSize, PipeReader reader, CancellationToken cancellationToken )
        {
            await AutoDisconnectAsync( DisconnectedReason.ProtocolError );
            throw new ProtocolViolationException();
        }

        /// <inheritdoc/>
        public void SetMessageHandler( MessageHandlerDelegate messageHandler )
        {
            _messageHandler = messageHandler;
        }

        protected override async ValueTask OnClosingAsync( DisconnectedReason reason )
        {
            if( reason == DisconnectedReason.UserDisconnected )
            {
                await OutputPump!.SendMessageAsync( OutgoingDisconnect.Instance );
            }
        }

        protected override void OnClosed( DisconnectedReason reason )
        {
            _channel!.Close( _config.InputLogger );
            _channel = null;
            if( reason != DisconnectedReason.None
                && (_config.DisconnectBehavior & DisconnectBehavior.CancelAcksOnDisconnect) == DisconnectBehavior.CancelAcksOnDisconnect )
            {
                _store!.IdStore.ResetAndCancelTasks();
            }
            _store = null;
            base.OnClosed( reason );
        }

        public ValueTask<Task<T?>> SendPacket<T>( IActivityMonitor m, IOutgoingPacketWithId outgoingPacket ) where T : class
        {
            ThrowIfNotConnected();
            return SenderHelper.SendPacket<T>( m, _store, OutputPump!, outgoingPacket, _config );
        }
    }
}
