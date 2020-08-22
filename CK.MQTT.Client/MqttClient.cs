using System;
using System.Net;
using System.Threading.Tasks;
using System.Text;
using System.Buffers;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Collections.Generic;
using static CK.MQTT.IMqttClient;
using CK.Core;

namespace CK.MQTT
{
    /// <inheritdoc cref="IMqttClient"/>
    public class MqttClient : IMqttClient
    {
        //Dont change between lifecycles
        readonly MqttConfiguration _config;
        readonly IMqttChannelFactory _channelFactory;

        //change between lifecycles
        bool _closed = true;
        IMqttChannel? _channel;
        IncomingMessageHandler? _input;
        OutgoingMessageHandler? _output;
        IPacketIdStore? _packetIdStore;
        PacketStore? _store;
        /// <summary>
        /// Instantiate the <see cref="MqttClient"/> with the given configuration.
        /// </summary>
        /// <param name="config">The config to use.</param>
        public MqttClient( MqttConfiguration config, MessageHandlerDelegate messageHandler )
        {
            _config = config;
            MessageHandler = messageHandler;
            _channelFactory = config.ChannelFactory;
        }

        T ThrowIfNotConnected<T>( [NotNull] T? item ) where T : class
        {
            if( _closed ) throw new InvalidOperationException( "Client is Disconnected." );
            return item ?? throw new NullReferenceException( "Blame Kuinox" );
        }
        ValueTask OnMessage( IncomingMessage msg ) => MessageHandler( msg );

        /// <inheritdoc/>
        public async Task<ConnectResult> ConnectAsync( IActivityMonitor m, MqttClientCredentials? credentials = null, OutgoingLastWill? lastWill = null )
        {
            (_store, _packetIdStore) = await _config.StoreFactory.CreateAsync( m, _config, _config.ConnectionString, credentials?.CleanSession ?? true );

            _channel = await _channelFactory.CreateAsync( m, _config.ConnectionString );
            ConnectAckReflex connectAckReflex = new ConnectAckReflex();
            Task<ConnectResult> connectedTask = connectAckReflex.Task;
            _input = new IncomingMessageHandler( _config, CloseSelfAsync, PipeReader.Create( _channel.Stream, _config.ReaderOptions ), connectAckReflex.ProcessIncomingPacket );
            PingRespReflex pingRes = new PingRespReflex( _config, _input );
            _output = new OutgoingMessageHandler( pingRes, CloseSelfAsync, PipeWriter.Create( _channel.Stream, _config.WriterOptions ), _config, _store );
            connectAckReflex.Reflex = new ReflexMiddlewareBuilder()
                .UseMiddleware( new PublishReflex( _packetIdStore, OnMessage, _output ) )
                .UseMiddleware( new PublishLifecycleReflex( _packetIdStore, _store, _output ) )
                .UseMiddleware( new SubackReflex( _store ) )
                .UseMiddleware( new UnsubackReflex( _store ) )
                .UseMiddleware( pingRes )
                .Build( InvalidPacket );
            await _output.SendMessageAsync( new OutgoingConnect( ProtocolConfiguration.Mqtt3, _config, credentials, lastWill ) );
            await Task.WhenAny( connectedTask, Task.Delay( _config.WaitTimeoutMs ) );
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
            _closed = false;
            return res;
        }

        async static Task SendAllStoredMessages( IActivityMonitor m, PacketStore store, OutgoingMessageHandler output )
        {
            IAsyncEnumerable<IOutgoingPacketWithId> msgs = await store!.GetAllMessagesAsync( m );
            await foreach( IOutgoingPacketWithId msg in msgs )
            {
                await output!.SendMessageAsync( msg );
            }
        }


        async ValueTask InvalidPacket( IInputLogger? m, IncomingMessageHandler sender, byte header, int packetSize, PipeReader reader )
        {
            await CloseSelfAsync( DisconnectedReason.ProtocolError );
            throw new ProtocolViolationException();
        }

        /// <inheritdoc/>
        public MessageHandlerDelegate MessageHandler { get; set; }

        Task CloseHandlers() => Task.WhenAll(  _input!.CloseAsync(), _output!.CloseAsync() );

        readonly object _lock = new object();
        async Task CloseSelfAsync( DisconnectedReason reason )
        {
            lock( _lock )
            {
                if( _closed ) return;
                _closed = true;
            }
            await CloseHandlers();
            _config.InputLogger?.ClientSelfClosing( reason );
            ThrowIfNotConnected( _channel ).Close( _config.InputLogger );
            _closed = true;
            DisconnectedHandler?.Invoke( reason );
        }

        async Task CloseUser()
        {
            lock( _lock )
            {
                if( _closed ) return;
                _closed = true;
            }
            await CloseHandlers();//we closed the loop, we can safely use on of it's logger.
            _channel!.Close( _config.InputLogger );
        }

        /// <inheritdoc/>
        public Disconnected? DisconnectedHandler { get; set; }

        /// <inheritdoc/>
        public bool IsConnected => !_closed && (_channel?.IsConnected ?? false);


        /// <inheritdoc/>
        public async ValueTask DisconnectAsync()
        {
            await await ThrowIfNotConnected( _output ).SendMessageAsync( OutgoingDisconnect.Instance );
            await CloseUser();
        }

        /// <inheritdoc/>
        public async ValueTask<Task> PublishAsync( IActivityMonitor m, OutgoingApplicationMessage message )
            => await SenderHelper.SendPacket<object>( m, ThrowIfNotConnected( _store ), ThrowIfNotConnected( _output ), message, _config );

        /// <inheritdoc/>
        public ValueTask<Task<SubscribeReturnCode[]?>> SubscribeAsync( IActivityMonitor m, params Subscription[] subscriptions )
            => SenderHelper.SendPacket<SubscribeReturnCode[]>( m, ThrowIfNotConnected( _store ), ThrowIfNotConnected( _output ), new OutgoingSubscribe( subscriptions ), _config );

        /// <inheritdoc/>
        public async ValueTask<Task> UnsubscribeAsync( IActivityMonitor m, params string[] topics )
            => await SenderHelper.SendPacket<object>( m, ThrowIfNotConnected( _store ), ThrowIfNotConnected( _output ), new OutgoingUnsubscribe( topics ), _config );
    }
}
