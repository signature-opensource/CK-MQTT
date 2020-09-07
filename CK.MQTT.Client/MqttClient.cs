using System;
using System.Net;
using System.Threading.Tasks;
using System.IO.Pipelines;
using System.Collections.Generic;
using CK.Core;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using CK.MQTT.Common.Pumps;

namespace CK.MQTT
{
    public class MqttClient : IMqttClient
    {
        readonly ProtocolConfiguration _pConfig;

        //Dont change between lifecycles
        readonly MqttConfiguration _config;
        readonly IMqttChannelFactory _channelFactory;

        //change between lifecycles
        CancellationTokenSource _closed = new CancellationTokenSource( 0 );
        IMqttChannel? _channel;
        InputPump? _input;
        OutputPump? _output;
        IPacketIdStore? _packetIdStore;
        PacketStore? _store;
        MessageHandlerDelegate _messageHandler;

        /// <summary>
        /// Instantiate the <see cref="MqttClient"/> with the given configuration.
        /// </summary>
        /// <param name="config">The config to use.</param>
        /// <param name="messageHandler">The delegate that will handle incoming messages. <see cref="MessageHandlerDelegate"/> docs for more info.</param>
        MqttClient( ProtocolConfiguration pConfig, MqttConfiguration config, MessageHandlerDelegate messageHandler )
        {
            _pConfig = pConfig;
            _config = config;
            _messageHandler = messageHandler;
            _channelFactory = config.ChannelFactory;
        }

        public static IMqtt3Client CreateMQTT3Client( MqttConfiguration config, MessageHandlerDelegate messageHandler ) =>
            new MqttClient( ProtocolConfiguration.Mqtt3, config, messageHandler );
        public static IMqtt5Client CreateMQTT5Client( MqttConfiguration config, MessageHandlerDelegate messageHandler ) =>
            new MqttClient( ProtocolConfiguration.Mqtt5, config, messageHandler );
        public static IMqttClient CreateMQTTClient( MqttConfiguration config, MessageHandlerDelegate messageHandler )
            => new MqttClient( ProtocolConfiguration.Mqtt5, config, messageHandler );

        [MemberNotNull( nameof( _input ) )]
        [MemberNotNull( nameof( _output ) )]
        [MemberNotNull( nameof( _packetIdStore ) )]
        [MemberNotNull( nameof( _store ) )]
        void ThrowIfNotConnected()
        {
            if( _closed.IsCancellationRequested ) throw new InvalidOperationException( "Client is Disconnected." );
            Debug.Assert( _input != null );
            Debug.Assert( _output != null );
            Debug.Assert( _packetIdStore != null );
            Debug.Assert( _store != null );
        }

        /// <summary>
        /// This method is required so the delegate used in the Reflex doesn't change.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        ValueTask OnMessage( string topic, PipeReader pipeReader, int payloadLength, QualityOfService qos, bool retain )
            => _messageHandler( topic, pipeReader, payloadLength, qos, retain );

        /// <inheritdoc/>
        public async Task<ConnectResult> ConnectAsync( IActivityMonitor m, MqttClientCredentials? credentials = null, OutgoingLastWill? lastWill = null )
        {
            (_store, _packetIdStore) = await _config.StoreFactory.CreateAsync( m, _pConfig, _config, _config.ConnectionString, credentials?.CleanSession ?? true );

            _channel = await _channelFactory.CreateAsync( m, _config.ConnectionString );
            ConnectAckReflex connectAckReflex = new ConnectAckReflex();
            Task<ConnectResult> connectedTask = connectAckReflex.Task;
            _input = new InputPump( _config, CloseSelfAsync, _channel.DuplexPipe.Input, connectAckReflex.ProcessIncomingPacket );
            _output = new OutputPump( DumbOutputProcessor.OutputProcessor, CloseSelfAsync, _channel.DuplexPipe.Output, _pConfig, _config, _store );
            PingRespReflex pingRes = new PingRespReflex( _config, _input );
            connectAckReflex.Reflex = new ReflexMiddlewareBuilder()
                .UseMiddleware( new PublishReflex( _packetIdStore, OnMessage, _output ) )
                .UseMiddleware( new PublishLifecycleReflex( _packetIdStore, _store, _output ) )
                .UseMiddleware( new SubackReflex( _store ) )
                .UseMiddleware( new UnsubackReflex( _store ) )
                .UseMiddleware( pingRes )
                .Build( InvalidPacket );
            _closed = new CancellationTokenSource();
            await _output.SendMessageAsync( new OutgoingConnect( _pConfig, _config, credentials, lastWill ) );
            _output.SetOutputProcessor( new MainOutputProcessor( _config, _store, pingRes ).OutputProcessor );
            await Task.WhenAny( connectedTask, Task.Delay( _config.WaitTimeout, _closed.Token ) );
            if( _closed.IsCancellationRequested )
            {
                await CloseUser();
                return new ConnectResult( ConnectError.RemoteDisconnected );
            }
            if( !connectedTask.IsCompleted )
            {
                await CloseUser();//We don't want to raise disconnect event if it fail to connect.
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
                await SendAllStoredMessages( m, _store, _output );
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


        async ValueTask InvalidPacket( IInputLogger? m, InputPump sender, byte header, int packetSize, PipeReader reader )
        {
            await CloseSelfAsync( DisconnectedReason.ProtocolError );
            throw new ProtocolViolationException();
        }

        /// <inheritdoc/>
        public void SetMessageHandler( MessageHandlerDelegate messageHandler )
        {
            _messageHandler = messageHandler;
        }

        Task CloseHandlers() => Task.WhenAll( _input!.CloseAsync(), _output!.CloseAsync() );

        readonly object _lock = new object();
        async Task CloseSelfAsync( DisconnectedReason reason )
        {
            lock( _lock )
            {
                if( _closed.IsCancellationRequested ) return;
                _closed.Cancel();
            }
            await CloseHandlers();
            _config.InputLogger?.ClientSelfClosing( reason );
            _channel!.Close( _config.InputLogger );
            DisconnectedHandler?.Invoke( reason );
        }

        async Task CloseUser()
        {
            lock( _lock )
            {
                if( _closed.IsCancellationRequested ) return;
                _closed.Cancel();
            }
            await CloseHandlers();//we closed the loop, we can safely use on of it's logger.
            _channel!.Close( _config.InputLogger );
        }

        /// <inheritdoc/>
        public Disconnected? DisconnectedHandler { get; set; }

        /// <inheritdoc/>
        public bool IsConnected => !_closed.IsCancellationRequested && (_channel?.IsConnected ?? false);


        /// <inheritdoc/>
        public async ValueTask DisconnectAsync()
        {
            ThrowIfNotConnected();
            await _output.SendMessageAsync( OutgoingDisconnect.Instance );
            await CloseUser();
        }

        public ValueTask<Task<T?>> SendPacket<T>( IActivityMonitor m, IOutgoingPacketWithId outgoingPacket ) where T : class
        {
            ThrowIfNotConnected();
            return SenderHelper.SendPacket<T>( m, _store, _output, outgoingPacket, _config );
        }
    }
}
