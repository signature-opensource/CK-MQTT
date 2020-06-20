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
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace CK.MQTT.Client
{
    public class MqttClient : IMqttClient
    {
        readonly IPacketIdStore _packetIdStore;
        readonly PacketStore _store;
        readonly MqttConfiguration _config;
        IMqttChannel? _channel;
        readonly IMqttChannelFactory _channelFactory;
        readonly OutgoingMessageHandler _output;
        private readonly StreamPipeReaderOptions? _readerOptions;
        private readonly StreamPipeWriterOptions? _writerOptions;
        IncomingMessageHandler? _input;
        [NotNull]
        Reflex? _postConnectReflex;
        PipeWriter? _pipeWriter;
        PipeReader? _pipeReader;
        MqttClient(
            IPacketIdStore packetIdStore,
            PacketStore store,
            MqttConfiguration mqttConfiguration,
            IMqttChannelFactory channelFactory,
            OutgoingMessageHandler outgoingHandler,
            StreamPipeReaderOptions? readerOptions = null,
            StreamPipeWriterOptions? writerOptions = null )
        {
            _packetIdStore = packetIdStore;
            _store = store;
            _config = mqttConfiguration;
            _readerOptions = readerOptions;
            _writerOptions = writerOptions;
            _channelFactory = channelFactory;
            _output = outgoingHandler;
        }
        public static IMqttClient Create(
            IPacketIdStore packetIdStore,
            PacketStore store,
            MqttConfiguration mqttConfiguration,
            IMqttChannelFactory channelFactory,
            int channelCapacity = 32,
            StreamPipeReaderOptions? readerOptions = null,
            StreamPipeWriterOptions? writerOptions = null )
        {
            var messageChannel = Channel.CreateBounded<IOutgoingPacket>( channelCapacity );
            var reflexChannel = Channel.CreateBounded<IOutgoingPacket>( channelCapacity );
            ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder();//I think there is a code smell there, object referencing each other make the instantiation complex.
            ushort keepAlive = mqttConfiguration.KeepAliveSecs;
            var output = new OutgoingMessageHandler( messageChannel, reflexChannel );
            KeepAliveTimer? timer = null;
            if( keepAlive != 0 )
            {
                timer = new KeepAliveTimer( TimeSpan.FromSeconds( keepAlive ), TimeSpan.FromMilliseconds( mqttConfiguration.KeepAliveSecs ), output );
                output.OutputMiddleware = timer!.OutputTransformer;
            }
            var client = new MqttClient( packetIdStore, store, mqttConfiguration, channelFactory, output, readerOptions, writerOptions );
            if( timer != null ) timer.TimeoutCallback = client.PingReqTimeout;
            builder.UseMiddleware( new PublishReflex( packetIdStore, client.OnMessage, output ) );
            builder.UseMiddleware( new PubackReflex( store ) );
            builder.UseMiddleware( new PubReceivedReflex( store, output ) );
            builder.UseMiddleware( new PubRelReflex( store, output ) );
            builder.UseMiddleware( new SubackReflex( store ) );
            builder.UseMiddleware( new UnsubackReflex( store ) );
            builder.UseMiddleware( new PingRespReflex( timer is null ? (Action?)null : timer.ResetTimer ) );
            client._postConnectReflex = builder.Build( client.InvalidPacket );
            return client;
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
                msg.PipeReader.
                m.Info( $"Received message: Topic'{msg.Topic}' Payload:'{Encoding.UTF8.GetString(}'" );
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

        void Close( IActivityMonitor m, DisconnectedReason disconnectedReason )
        {
            _channel.Close( m );
            _eDisconnected.Raise( m, this, new MqttEndpointDisconnected( disconnectedReason ) );
        }

        public bool IsConnected => _channel?.IsConnected ?? false;

        public async Task<ConnectResult> ConnectAsync( IActivityMonitor m, MqttClientCredentials? credentials = null, OutgoingLastWill? lastWill = null )
        {
            _channel = _channelFactory.Create( _config.ConnectionString );
            if( !_channel.IsConnected )
            {
                m.Error( "Channel Factory Created an unconnected channel !" );
                return new ConnectResult( ConnectError.ChannelNotConnected, SessionState.Unknown, ConnectReturnCode.Unknown );
            }
            _pipeReader = PipeReader.Create( _channel.Stream, _readerOptions );
            _pipeWriter = PipeWriter.Create( _channel.Stream, _writerOptions );
            ConnectAckReflex connectReflex = new ConnectAckReflex( _postConnectReflex );
            Task<ConnectResult> connectedTask = connectReflex.Task;
            _input = new IncomingMessageHandler( _pipeReader, connectReflex.ProcessIncomingPacket );
            _output.SetPipeWriter( _pipeWriter );
            var outgoingConnect = new OutgoingConnect( ProtocolConfiguration.Mqtt3, _config, credentials, lastWill );
            await _output.SendMessageAsync( outgoingConnect );
            await Task.WhenAny( connectedTask, Task.Delay( _config.WaitTimeoutMiliseconds ) );
            if( !connectedTask.IsCompleted )
            {
                return new ConnectResult( ConnectError.Timeout, SessionState.Unknown, ConnectReturnCode.Unknown );
            }
            return await connectedTask;
        }

        public async ValueTask DisconnectAsync( IActivityMonitor m ) => await _output.SendMessageAsync( new OutgoingDisconnect() );

        public async ValueTask<Task> PublishAsync( IActivityMonitor m, OutgoingApplicationMessage message )
            => await SendQoSPacketProcess.SendPacket<object>( m, _store, _output, message, _config.WaitTimeoutMiliseconds );
        //await required to cast the Task<object> to Task
        public ValueTask<Task<SubscribeReturnCode[]?>> SubscribeAsync( IActivityMonitor m, params Subscription[] subscriptions )
        {
            OutgoingSubscribe packet = new OutgoingSubscribe( subscriptions );
            return SendQoSPacketProcess.SendPacket<SubscribeReturnCode[]>( m, _store, _output, packet, _config.WaitTimeoutMiliseconds );
        }

        public async ValueTask<Task> UnsubscribeAsync( IActivityMonitor m, params string[] topics )
        {
            OutgoingUnsubscribe packet = new OutgoingUnsubscribe( topics );
            return await SendQoSPacketProcess.SendPacket<object>( m, _store, _output, packet, _config.WaitTimeoutMiliseconds );
        }

        public Task<IncomingApplicationMessage?> WaitMessageReceivedAsync( Func<IncomingApplicationMessage, bool>? predicate = null, int timeoutMillisecond = -1 )
        {
            throw new NotImplementedException();
        }
    }
}
