using CK.MQTT.Client.OutgoingPackets;
using CK.MQTT.Client.Reflexes;
using CK.MQTT.Common;
using System;
using System.Net;
using System.Threading.Tasks;
using System.Text;
using System.Buffers;
using System.IO;
using System.Diagnostics.CodeAnalysis;

namespace CK.MQTT.Client
{
    public class MqttClient : IMqttClient
    {
        //Dont change between lifecycles
        readonly IPacketIdStore _packetIdStore;
        readonly PacketStore _store;
        readonly MqttConfiguration _config;
        readonly IMqttChannelFactory _channelFactory;
        readonly StreamPipeReaderOptions? _readerOptions;
        readonly StreamPipeWriterOptions? _writerOptions;

        //change between lifecycles
        bool _closed;
        IMqttChannel? _channel;
        IncomingMessageHandler? _input;
        OutgoingMessageHandler? _output;
        public MqttClient(
            IPacketIdStore packetIdStore,
            PacketStore store,
            MqttConfiguration mqttConfiguration,
            IMqttChannelFactory channelFactory,
            StreamPipeReaderOptions? readerOptions = null,
            StreamPipeWriterOptions? writerOptions = null )
        {
            _packetIdStore = packetIdStore;
            _store = store;
            _config = mqttConfiguration;
            _readerOptions = readerOptions;
            _writerOptions = writerOptions;
            _channelFactory = channelFactory;
        }

        T ThrowIfNull<T>( [NotNull] T? item ) where T : class
            => item ?? throw new InvalidOperationException( "Client is Disconnected." );

        public async ValueTask<Task<ConnectResult>> ConnectAsync( IActivityMonitor m,
            MqttClientCredentials? credentials = null, OutgoingLastWill? lastWill = null )
        {
            _channel = _channelFactory.Create( _config.ConnectionString );
            _output = new OutgoingMessageHandler( ( a, b ) => Close( a, b ), PipeWriter.Create( _channel.Stream, _writerOptions ), _config );
            KeepAliveTimer? timer = _config.KeepAliveSecs != 0 ? new KeepAliveTimer( _config, _output, PingReqTimeout ) : null;
            _output.OutputMiddleware = timer != null ? timer.OutputTransformer : (OutgoingMessageHandler.OutputTransformer?)null;
            ConnectAckReflex connectAckReflex = new ConnectAckReflex( new ReflexMiddlewareBuilder()
                .UseMiddleware( LogPacketTypeReflex.ProcessIncomingPacket )
                .UseMiddleware( new PublishReflex( _packetIdStore, OnMessage, _output ) )
                .UseMiddleware( new PubackReflex( _store ) )
                .UseMiddleware( new PubReceivedReflex( _store, _output ) )
                .UseMiddleware( new PubRelReflex( _store, _output ) )
                .UseMiddleware( new PubCompReflex( _store ) )
                .UseMiddleware( new SubackReflex( _store ) )
                .UseMiddleware( new UnsubackReflex( _store ) )
                .UseMiddleware( new PingRespReflex( timer is null ? (Action?)null : timer.ResetTimer ) )
                .Build( InvalidPacket ) );
            Task<ConnectResult> connectedTask = connectAckReflex.Task;
            _input = new IncomingMessageHandler( ( a, b ) => Close( a, b ), PipeReader.Create( _channel.Stream, _readerOptions ), connectAckReflex.ProcessIncomingPacket );
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
                ValueTask task = _packetIdStore.ResetAsync();
                await _store.ResetAsync();
                await task;
            }
            return res;
        }

        void PingReqTimeout( IActivityMonitor m ) => Close( m, DisconnectedReason.RemoteDisconnected );//TODO: clean disconnect, not close.

        async Task OnMessage( IActivityMonitor m, IncomingApplicationMessage msg )
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

        ValueTask InvalidPacket( IActivityMonitor m, IncomingMessageHandler sender, byte header, int packetSize, PipeReader reader )
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

        void Close( IActivityMonitor m, DisconnectedReason reason, string? message = null )
        {
            const DisconnectedReason reasonMax = (DisconnectedReason)int.MaxValue;
            if( reason == reasonMax ) throw new ArgumentException( nameof( reason ) );
            if( _closed ) return;
            _closed = true;
            _input!.Close( m, default );
            _input = null;
            _output!.Close( m, default );
            _output = null;
            _channel!.Close( m );
            _channel = null;
            _eDisconnected.Raise( m, this, new MqttEndpointDisconnected( reason, message ) );
        }

        public bool IsConnected => _channel?.IsConnected ?? false;

        public async ValueTask DisconnectAsync( IActivityMonitor m )
        {
            Task disconnect = await ThrowIfNull( _output ).SendMessageAsync( new OutgoingDisconnect() );
            _output.Complete();
            await disconnect;
            Close( m, DisconnectedReason.SelfDisconnected );
        }

        public async ValueTask<Task> PublishAsync( IActivityMonitor m, OutgoingApplicationMessage message )
            => await SenderHelper.SendPacket<object>( m, _store, ThrowIfNull( _output ), message, _config );

        public ValueTask<Task<SubscribeReturnCode[]?>> SubscribeAsync( IActivityMonitor m, params Subscription[] subscriptions )
            => SenderHelper.SendPacket<SubscribeReturnCode[]>( m, _store, ThrowIfNull( _output ), new OutgoingSubscribe( subscriptions ), _config );

        public async ValueTask<Task> UnsubscribeAsync( IActivityMonitor m, params string[] topics )
            => await SenderHelper.SendPacket<object>( m, _store, ThrowIfNull( _output ), new OutgoingUnsubscribe( topics ), _config );
    }
}
