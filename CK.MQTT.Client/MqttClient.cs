using CK.Core;
using CK.MQTT.Abstractions.Packets;
using CK.MQTT.Client.OutgoingPackets;
using CK.MQTT.Client.Reflexes;
using CK.MQTT.Common;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Reflexes;
using CK.MQTT.Common.Stores;
using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Channels;
using System.Threading.Tasks;
using CK.MQTT.Common.Processes;
using System.Text;
using System.Buffers;
using System.IO;
using System.Threading;

namespace CK.MQTT.Client
{
    public class MqttClient : IMqttClient
    {
        //Dont change between lifecycles
        readonly Channel<IOutgoingPacket> _reflexChannel;
        readonly Channel<IOutgoingPacket> _messageChannel;
        readonly IPacketIdStore _packetIdStore;
        readonly PacketStore _store;
        readonly MqttConfiguration _config;
        readonly IMqttChannelFactory _channelFactory;
        readonly StreamPipeReaderOptions? _readerOptions;
        readonly StreamPipeWriterOptions? _writerOptions;

        //change between lifecycles
        bool _connected;
        IMqttChannel? _channel;
        IncomingMessageHandler? _input;
        OutgoingMessageHandler? _output;
        public MqttClient(
            IPacketIdStore packetIdStore,
            PacketStore store,
            MqttConfiguration mqttConfiguration,
            IMqttChannelFactory channelFactory,
            int channelPacketCount,
            StreamPipeReaderOptions? readerOptions = null,
            StreamPipeWriterOptions? writerOptions = null )
        {
            _reflexChannel = Channel.CreateBounded<IOutgoingPacket>( channelPacketCount );
            _messageChannel = Channel.CreateBounded<IOutgoingPacket>( channelPacketCount );
            _packetIdStore = packetIdStore;
            _store = store;
            _config = mqttConfiguration;
            _readerOptions = readerOptions;
            _writerOptions = writerOptions;
            _channelFactory = channelFactory;
        }

        public async Task<ConnectResult> ConnectAsync( IActivityMonitor m, MqttClientCredentials? credentials = null, OutgoingLastWill? lastWill = null )
        {
            _channel = _channelFactory.Create( _config.ConnectionString );
            if( !_channel.IsConnected )
            {
                m.Error( "Channel Factory Created an unconnected channel !" );
                return new ConnectResult( ConnectError.ChannelNotConnected, SessionState.Unknown, ConnectReturnCode.Unknown );
            }
            PipeReader pipeReader = PipeReader.Create( _channel.Stream, _readerOptions );
            PipeWriter pipeWriter = PipeWriter.Create( _channel.Stream, _writerOptions );
            _output = new OutgoingMessageHandler( pipeWriter, _messageChannel, _reflexChannel );
            _output.Stopped += OnWriterDisconnected;
            ushort keepAlive = _config.KeepAliveSecs;
            KeepAliveTimer? timer = null;
            if( keepAlive != 0 )
            {
                timer = new KeepAliveTimer( TimeSpan.FromSeconds( keepAlive ), TimeSpan.FromMilliseconds( _config.WaitTimeoutMiliseconds ), _output, PingReqTimeout );
                _output.OutputMiddleware = timer.OutputTransformer;
            }
            ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder();//I think there is a code smell there, object referencing each other make the instantiation complex.
            builder.UseMiddleware( new PublishReflex( _packetIdStore, OnMessage, _output ) );
            builder.UseMiddleware( new PubackReflex( _store ) );
            builder.UseMiddleware( new PubReceivedReflex( _store, _output ) );
            builder.UseMiddleware( new PubRelReflex( _store, _output ) );
            builder.UseMiddleware( new SubackReflex( _store ) );
            builder.UseMiddleware( new UnsubackReflex( _store ) );
            builder.UseMiddleware( new PingRespReflex( timer is null ? (Action?)null : timer.ResetTimer ) );
            Reflex postConnectReflex = builder.Build( InvalidPacket );
            ConnectAckReflex connectReflex = new ConnectAckReflex( postConnectReflex );
            Task<ConnectResult> connectedTask = connectReflex.Task;
            _input = new IncomingMessageHandler( pipeReader, connectReflex.ProcessIncomingPacket );
            _connected = true;
            await new OutgoingConnect( ProtocolConfiguration.Mqtt3, _config, credentials, lastWill ).WriteAsync( pipeWriter, default );
            await Task.WhenAny( connectedTask, Task.Delay( _config.WaitTimeoutMiliseconds ) );
            if( !connectedTask.IsCompleted )
            {
                return new ConnectResult( ConnectError.Timeout, SessionState.Unknown, ConnectReturnCode.Unknown );
            }
            return await connectedTask;
        }

        void OnReaderDisconnected( IActivityMonitor m, IncomingMessageHandler sender, DisconnectedReason e ) => Close( m, e );

        void OnWriterDisconnected( IActivityMonitor m, OutgoingMessageHandler sender, Exception? e ) => Close( m, DisconnectedReason.RemoteDisconnected );

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
                int toRead = msg.PayloadLenght;
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

        async Task StopIO( IActivityMonitor m, CancellationToken dirtyStop )
        {
            var task = Task.WhenAll( _input.Stop( dirtyStop ), _output.Stop( dirtyStop ) );
            dirtyStop.Register( () => Close( m ) );
            await task;
            Close( m );
        }

        void Close( IActivityMonitor m )
        {
            _output.Stopped -= OnWriterDisconnected;
            _input.Disconnected -= OnReaderDisconnected;
            _channel.Close( m );
        }

        public bool IsConnected => _channel?.IsConnected ?? false;

        public async ValueTask DisconnectAsync( IActivityMonitor m, CancellationToken cancellationToken )
        {
            await _output.SendMessageAsync( new OutgoingDisconnect() );
        }

        public async ValueTask<Task> PublishAsync( IActivityMonitor m, OutgoingApplicationMessage message )
            => await SenderHelper.SendPacket<object>( m, _store, _output, message, _config );

        public ValueTask<Task<SubscribeReturnCode[]?>> SubscribeAsync( IActivityMonitor m, params Subscription[] subscriptions )
            => SenderHelper.SendPacket<SubscribeReturnCode[]>( m, _store, _output, new OutgoingSubscribe( subscriptions ), _config );

        public async ValueTask<Task> UnsubscribeAsync( IActivityMonitor m, params string[] topics )
            => await SenderHelper.SendPacket<object>( m, _store, _output, new OutgoingUnsubscribe( topics ), _config );
    }
}
