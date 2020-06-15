using CK.Core;
using CK.MQTT.Abstractions.Packets;
using CK.MQTT.Client.OutgoingPackets;
using CK.MQTT.Client.Processes;
using CK.MQTT.Client.Reflexes;
using CK.MQTT.Common;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Reflexes;
using CK.MQTT.Common.Stores;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
namespace CK.MQTT.Client
{
    class MqttClient : IMqttClient
    {
        bool _channelWasConnected;
        readonly IPacketStore _store;
        readonly MqttConfiguration _mqttConfiguration;
        readonly IMqttChannel _channel;
        readonly OutgoingMessageHandler? _outgoingHandler;
        readonly IncomingMessageHandler _incomingHandler;
        readonly PipeWriter _pipeWriter;
        readonly PipeReader _pipeReader;
        readonly int _waitTimeoutSecs;
        Reflex? _postConnectReflex;
        MqttClient(
            IPacketStore store,
            MqttConfiguration mqttConfiguration,
            IMqttChannel channel,
            IncomingMessageHandler incomingHandler,
            Reflex postConnectReflex,
            int waitTimeoutSecs,
            string clientId)
        {
            _store = store;
            _mqttConfiguration = mqttConfiguration;
            ClientId = clientId;
            _channel = channel;
            _outgoingHandler = outgoingHandler;
            _incomingHandler = incomingHandler;
            _postConnectReflex = postConnectReflex;
            _waitTimeoutSecs = waitTimeoutSecs;
        }
        [MemberNotNull(nameof(_postConnectReflex))]
        public static IMqttClient Create( IPacketStore store,
            MqttConfiguration mqttConfiguration,
            IMqttChannel channel,
            PipeWriter pipeWriter,
            Channel<OutgoingPacket> externalMessageChannel,
            Channel<OutgoingPacket> internalMessageChannel,
            IncomingMessageHandler incomingHandler,
            int waitTimeoutSecs,
            string clientId = "")
        {
            ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder();
            var output = new OutgoingMessageHandler(pipeWriter, o);
            new MqttClient( store, mqttConfiguration, channel, output, incomingHandler, postConnectReflex, waitTimeoutSecs, clientId );
            builder.UseMiddleware( new PublishReflex( store, ( m, msg ) => _eMessage.RaiseAsync( m, this, msg ), _outgoingHandler ) );
            builder.UseMiddleware( new PubackReflex( store ) );
            builder.UseMiddleware( new PubReceivedReflex( store, output ) );
            builder.UseMiddleware( new PubRelReflex( store, output ) );
            builder.UseMiddleware( new PingRespReflex(PingCalled) );
            var postConnectReflex = builder.Build( InvalidPacket );

            return
        }

        void PingCalled()
        {

        }

        ValueTask InvalidPacket( IActivityMonitor m, byte header, int packetSize, PipeReader reader )
        {
            Close( m, DisconnectedReason.ProtocolError );
            throw new ProtocolViolationException();
        }

        public string ClientId { get; private set; }

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
            _channelWasConnected = false;
            _channel.Close( m );
            _eDisconnected.Raise( m, this, new MqttEndpointDisconnected( disconnectedReason ) );
        }

        public bool IsConnected => _channel.IsConnected;

        public ValueTask<ConnectResult> ConnectAnonymousAsync( IActivityMonitor m )
            => ConnectProcess.ExecuteConnectProtocol(
                m,
                _outgoingHandler,
                _incomingHandler,
                new OutgoingConnect( ProtocolConfiguration.Mqtt3, _mqttConfiguration, new MqttClientCredentials(), true ),
                _waitTimeoutSecs, _postConnectReflex
                );

        public ValueTask<ConnectResult> ConnectAnonymousAsync( IActivityMonitor m, string topic, Func<PipeWriter, ValueTask> payloadWriter )
        {
            throw new NotImplementedException();
        }

        public ValueTask<ConnectResult> ConnectAsync( IActivityMonitor m, MqttClientCredentials credentials, bool cleanSession = false )
        {
            throw new NotImplementedException();
        }

        public ValueTask<ConnectResult> ConnectAsync( IActivityMonitor m, MqttClientCredentials credentials, string topic, Func<PipeWriter, ValueTask> payloadWriter )
        {
            throw new NotImplementedException();
        }

        public ValueTask DisconnectAsync( IActivityMonitor m )
        {
            throw new NotImplementedException();
        }

        public ValueTask<ValueTask> PublishAsync( IActivityMonitor m, OutgoingApplicationMessage message, string topic, ReadOnlyMemory<byte> payload, QualityOfService qos, bool retain = false )
        {
            throw new NotImplementedException();
        }

        public ValueTask<ValueTask<IReadOnlyCollection<SubscribeReturnCode>?>> SubscribeAsync( IActivityMonitor m, params Subscription[] subscriptions )
        {
            throw new NotImplementedException();
        }

        public ValueTask<ValueTask<bool>> UnsubscribeAsync( IActivityMonitor m, IEnumerable<string> topics )
        {
            throw new NotImplementedException();
        }

        public ValueTask<IncomingApplicationMessage?> WaitMessageReceivedAsync( Func<IncomingApplicationMessage, bool>? predicate = null, int timeoutMillisecond = -1 )
        {
            throw new NotImplementedException();
        }
    }
}
