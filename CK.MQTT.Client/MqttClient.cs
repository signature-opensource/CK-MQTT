using System;
using System.Net;
using System.Threading.Tasks;
using System.Text;
using System.Buffers;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;

namespace CK.MQTT
{
    public class MqttClient : IMqttClient, IDisposable
    {
        //Dont change between lifecycles
        readonly MqttConfiguration _config;
        readonly IMqttChannelFactory _channelFactory;

        //change between lifecycles
        bool _closed;
        IMqttChannel? _channel;
        IncomingMessageHandler? _input;
        OutgoingMessageHandler? _output;
        IPacketIdStore? _packetIdStore;
        PacketStore? _store;
        public MqttClient( MqttConfiguration config )
        {
            _config = config;
            _channelFactory = config.ChannelFactory;
        }

        T ThrowIfNull<T>( [NotNull] T? item ) where T : class
            => item ?? throw new InvalidOperationException( "Client is Disconnected." );

        public async ValueTask<Task<ConnectResult>> ConnectAsync( IMqttLogger m, MqttClientCredentials? credentials = null, OutgoingLastWill? lastWill = null )
        {
            (_store, _packetIdStore) = await _config.StoreFactory.CreateAsync( m, _config.StoreTransformer, _config.ConnectionString, credentials?.CleanSession ?? true );

            _channel = await _channelFactory.CreateAsync( m, _config.ConnectionString );
            _output = new OutgoingMessageHandler( _config.LoggerFactory, ( a, b ) => Close( a, b ), PipeWriter.Create( _channel.Stream, _config.WriterOptions ), _config );
            KeepAliveTimer? timer = _config.KeepAliveSecs != 0 ? new KeepAliveTimer( _config.LoggerFactory, _config, _output, PingReqTimeout ) : null;
            _output.OutputMiddleware = timer != null ? timer.OutputTransformer : (OutgoingMessageHandler.OutputTransformer?)null;
            ConnectAckReflex connectAckReflex = new ConnectAckReflex( new ReflexMiddlewareBuilder()
                .UseMiddleware( new PublishReflex( _packetIdStore, OnMessage, _output ) )
                .UseMiddleware( new PublishLifecycleReflex( _store, _output ) )
                .UseMiddleware( new SubackReflex( _store ) )
                .UseMiddleware( new UnsubackReflex( _store ) )
                .UseMiddleware( new PingRespReflex( timer is null ? (Action?)null : timer.ResetTimer ) )
                .Build( InvalidPacket ) );
            Task<ConnectResult> connectedTask = connectAckReflex.Task;
            _input = new IncomingMessageHandler( _config.LoggerFactory, ( a, b ) => Close( a, b ), PipeReader.Create( _channel.Stream, _config.ReaderOptions ), connectAckReflex.ProcessIncomingPacket );
            _closed = true;
            await _output.SendMessageAsync( new OutgoingConnect( ProtocolConfiguration.Mqtt3, _config, credentials, lastWill ) );
            return InternalConnectAsync( connectedTask );
        }

        async Task<ConnectResult> InternalConnectAsync( Task<ConnectResult> connectedTask )
        {
            await Task.WhenAny( connectedTask, Task.Delay( _config.WaitTimeoutMs ) );
            if( !connectedTask.IsCompleted ) return new ConnectResult( ConnectError.Timeout );
            ConnectResult res = await connectedTask;
            if( res.SessionState == SessionState.CleanSession )
            {
                ValueTask task = ThrowIfNull( _packetIdStore ).ResetAsync();
                await ThrowIfNull( _store ).ResetAsync();
                await task;
            }
            return res;
        }

        void PingReqTimeout( IMqttLogger m ) => Close( m, DisconnectedReason.RemoteDisconnected );//TODO: clean disconnect, not close.

        async Task OnMessage( IMqttLogger m, IncomingApplicationMessage msg )
        {
            if( _eMessage.HasHandlers )
            {
                await _eMessage.RaiseAsync( m, this, msg );
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

        readonly SequentialEventHandlerSender<IMqttClient, MqttEndpointDisconnected> _eDisconnected
            = new SequentialEventHandlerSender<IMqttClient, MqttEndpointDisconnected>();
        public event SequentialEventHandler<IMqttClient, MqttEndpointDisconnected> Disconnected
        {
            add => _eDisconnected.Add( value );
            remove => _eDisconnected.Remove( value );
        }
        readonly SequentialEventHandlerAsyncSender<IMqttClient, IncomingApplicationMessage> _eMessage
            = new SequentialEventHandlerAsyncSender<IMqttClient, IncomingApplicationMessage>();
        public event SequentialEventHandlerAsync<IMqttClient, IncomingApplicationMessage> MessageReceivedAsync
        {
            add => _eMessage.Add( value );
            remove => _eMessage.Remove( value );
        }

        readonly object _lock = new object();
        void Close( IMqttLogger m, DisconnectedReason reason, string? message = null )
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
            _eDisconnected.Raise( m, this, new MqttEndpointDisconnected( reason, message ) );
        }

        public bool IsConnected => _channel?.IsConnected ?? false;

        public async ValueTask DisconnectAsync( IMqttLogger m )
        {
            Task disconnect = await ThrowIfNull( _output ).SendMessageAsync( new OutgoingDisconnect() );
            await _output.Complete();
            await disconnect;
            Close( m, DisconnectedReason.SelfDisconnected );
        }
        public void Dispose()
        {
            _input?.Dispose();
            _output?.Dispose();
        }
        public async ValueTask<Task> PublishAsync( IMqttLogger m, OutgoingApplicationMessage message )
            => await SenderHelper.SendPacket<object>( m, ThrowIfNull( _store ), ThrowIfNull( _output ), message, _config );

        public ValueTask<Task<SubscribeReturnCode[]?>> SubscribeAsync( IMqttLogger m, params Subscription[] subscriptions )
            => SenderHelper.SendPacket<SubscribeReturnCode[]>( m, ThrowIfNull( _store ), ThrowIfNull( _output ), new OutgoingSubscribe( subscriptions ), _config );

        public async ValueTask<Task> UnsubscribeAsync( IMqttLogger m, params string[] topics )
            => await SenderHelper.SendPacket<object>( m, ThrowIfNull( _store ), ThrowIfNull( _output ), new OutgoingUnsubscribe( topics ), _config );
    }
}
