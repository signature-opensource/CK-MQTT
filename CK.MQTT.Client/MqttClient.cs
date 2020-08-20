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

namespace CK.MQTT
{
    /// <inheritdoc cref="IMqttClient"/>
    public class MqttClient : IMqttClient, IDisposable
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
        public MqttClient( MqttConfiguration config )
        {
            _config = config;
            _channelFactory = config.ChannelFactory;
        }

        T ThrowIfNotConnected<T>( [NotNull] T? item ) where T : class
        {
            if( _closed ) throw new InvalidOperationException( "Client is Disconnected." );
            return item ?? throw new NullReferenceException( "Blame Kuinox" );
        }

        /// <inheritdoc/>
        public async Task<ConnectResult> ConnectAsync( IMqttLogger m, MqttClientCredentials? credentials = null, OutgoingLastWill? lastWill = null )
        {
            (_store, _packetIdStore) = await _config.StoreFactory.CreateAsync( m, _config.StoreTransformer, _config.ConnectionString, credentials?.CleanSession ?? true );

            _channel = await _channelFactory.CreateAsync( m, _config.ConnectionString );
            _output = new OutgoingMessageHandler( ( a, b ) => Close( a, b ), PipeWriter.Create( _channel.Stream, _config.WriterOptions ), _config, _store );
            ConnectAckReflex connectAckReflex = new ConnectAckReflex( new ReflexMiddlewareBuilder()
                .UseMiddleware( new PublishReflex( _packetIdStore, OnMessage, _output ) )
                .UseMiddleware( new PublishLifecycleReflex( _store, _output ) )
                .UseMiddleware( new SubackReflex( _store ) )
                .UseMiddleware( new UnsubackReflex( _store ) )
                .UseMiddleware( new PingRespReflex( timer is null ? (Action?)null : timer.ResetTimer ) )
                .Build( InvalidPacket ) );
            Task<ConnectResult> connectedTask = connectAckReflex.Task;
            _input = new IncomingMessageHandler( _config.InputLogger, ( a, b ) => Close( a, b ), PipeReader.Create( _channel.Stream, _config.ReaderOptions ), connectAckReflex.ProcessIncomingPacket );
            await _output.SendMessageAsync( new OutgoingConnect( ProtocolConfiguration.Mqtt3, _config, credentials, lastWill ) );
            await Task.WhenAny( connectedTask, Task.Delay( _config.WaitTimeoutMs ) );
            if( !connectedTask.IsCompleted )
            {
                Close( m, DisconnectedReason.UnspecifiedError, raiseEvent: false );
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

        async static Task SendAllStoredMessages( IMqttLogger m, PacketStore store, OutgoingMessageHandler output )
        {
            IAsyncEnumerable<IOutgoingPacketWithId> msgs = await store!.GetAllMessagesAsync( m );
            await foreach( IOutgoingPacketWithId msg in msgs )
            {
                await output!.SendMessageAsync( msg );
            }
        }

        void PingReqTimeout( IMqttLogger m ) => Close( m, DisconnectedReason.RemoteDisconnected );//TODO: clean disconnect, not close.

        async Task OnMessage( IMqttLogger m, IncomingMessage msg )
        {
            var handler = MessageHandler;
            if( handler != null )
            {
                await handler( m, msg );
                return;
            }
            else
            {
                int toRead = msg.PayloadLength;
                using( m.OpenInfo( $"Received message: Topic'{msg.Topic}'" ) )
                {
                    MemoryStream stream = new MemoryStream();
                    while( toRead > 0 )
                    {
                        var result = await msg.PipeReader.ReadAsync();
                        var buffer = result.Buffer;
                        if( buffer.Length > toRead )
                        {
                            buffer = buffer.Slice( 0, toRead );
                        }
                        toRead -= (int)buffer.Length;
                        stream.Write( buffer.ToArray() );
                        msg.PipeReader.AdvanceTo( buffer.End );
                    }
                    stream.Position = 0;
                    m.Info( Encoding.UTF8.GetString( stream.ToArray() ) );
                }
            }
        }

        ValueTask InvalidPacket( IMqttLogger m, IncomingMessageHandler sender, byte header, int packetSize, PipeReader reader )
        {
            Close( m, DisconnectedReason.ProtocolError );
            throw new ProtocolViolationException();
        }

        /// <inheritdoc/>
        public MessageHandlerDelegate? MessageHandler { get; set; }

        readonly object _lock = new object();
        void Close( IMqttLogger m, DisconnectedReason reason, string? message = null, bool raiseEvent = true )
        {
            const DisconnectedReason reasonMax = (DisconnectedReason)int.MaxValue;
            if( reason == reasonMax ) throw new ArgumentException( nameof( reason ) );
            lock( _lock )
            {
                if( _closed ) return;
                _closed = true;
            }
            _output!.Close( m, default );
            _input!.Close( m, default );
            _output.Dispose();
            _input.Dispose();
            _channel!.Close( m );
            _channel = null;
            _input = null;
            _output = null;
            if( raiseEvent ) DisconnectedHandler?.Invoke( m, new MqttEndpointDisconnected( reason, message ) );
        }
        /// <inheritdoc/>
        public Disconnected? DisconnectedHandler { get; set; }

        /// <inheritdoc/>
        public bool IsConnected => !_closed && (_channel?.IsConnected ?? false);


        /// <inheritdoc/>
        public async ValueTask DisconnectAsync( IMqttLogger m )
        {
            await await ThrowIfNotConnected( _output ).SendMessageAsync( OutgoingDisconnect.Instance );
                Close( m, DisconnectedReason.SelfDisconnected );
            if( !_closed )
            {
                m.Warn( "Client is not closed after completing to send messages." );
            }
        }

        /// <summary>
        /// Dispose the <see cref="Pipe"/>s used to operate.
        /// </summary>
        public void Dispose()
        {
            _input?.Dispose();
            _output?.Dispose();
        }
        /// <inheritdoc/>
        public async ValueTask<Task> PublishAsync( IMqttLogger m, OutgoingApplicationMessage message )
            => await SenderHelper.SendPacket<object>( m, ThrowIfNotConnected( _store ), ThrowIfNotConnected( _output ), message, _config );

        /// <inheritdoc/>
        public ValueTask<Task<SubscribeReturnCode[]?>> SubscribeAsync( IMqttLogger m, params Subscription[] subscriptions )
            => SenderHelper.SendPacket<SubscribeReturnCode[]>( m, ThrowIfNotConnected( _store ), ThrowIfNotConnected( _output ), new OutgoingSubscribe( subscriptions ), _config );

        /// <inheritdoc/>
        public async ValueTask<Task> UnsubscribeAsync( IMqttLogger m, params string[] topics )
            => await SenderHelper.SendPacket<object>( m, ThrowIfNotConnected( _store ), ThrowIfNotConnected( _output ), new OutgoingUnsubscribe( topics ), _config );
    }
}
