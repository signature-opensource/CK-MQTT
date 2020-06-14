using CK.Core;
using CK.MQTT.Abstractions.Packets;
using CK.MQTT.Client.OutgoingPackets;
using CK.MQTT.Client.Processes;
using CK.MQTT.Common;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.OutgoingPackets;
using CK.MQTT.Common.Packets;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static CK.MQTT.Common.Channels.IncomingMessageHandler;

namespace CK.MQTT.Client
{
    class MqttClient : IMqttClient
    {
        bool _channelWasConnected;
        readonly MqttConfiguration _mqttConfiguration;
        readonly IMqttChannel _channel;
        readonly OutgoingMessageHandler _outgoingHandler;
        readonly IncomingMessageHandler _incomingHandler;
        readonly PipeWriter _pipeWriter;
        readonly PipeReader _pipeReader;
        readonly Channel<OutgoingPacket> _regularChannel;
        readonly Channel<OutgoingPacket> _reflexChannel;
        readonly OutgoingMessageHandler.OutputTransformer _outputTransformer;
        readonly int _waitTimeoutSecs;
        public MqttClient(
            MqttConfiguration mqttConfiguration,
            string clientId,
            IMqttChannel channel,
            OutgoingMessageHandler outgoingHandler,
            IncomingMessageHandler incomingHandler,
            PipeWriter pipeWriter,
            PipeReader pipeReader,
            Channel<OutgoingPacket> regularChannel,
            Channel<OutgoingPacket> reflexChannel,
            Reflex inputReflex,
            OutgoingMessageHandler.OutputTransformer outputTransformer,
            int waitTimeoutSecs )
        {
            _mqttConfiguration = mqttConfiguration;
            ClientId = clientId;
            _channel = channel;
            _outgoingHandler = outgoingHandler;
            _incomingHandler = incomingHandler;
            _pipeWriter = pipeWriter;
            _pipeReader = pipeReader;
            _regularChannel = regularChannel;
            _reflexChannel = reflexChannel;
            _outputTransformer = outputTransformer;
            _waitTimeoutSecs = waitTimeoutSecs;
        }

        public string? ClientId { get; private set; }

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
            _eDisconnected.Raise( m, this, new MqttEndpointDisconnected( disconnectedReason );
        }

        public bool IsConnected => _channel.IsConnected;

        public ValueTask<ConnectResult> ConnectAnonymousAsync( IActivityMonitor m )
            => ConnectProcess.ExecuteConnectProtocol(
                m,
                _outgoingHandler,
                _incomingHandler,
                new OutgoingConnect( ProtocolConfiguration.Mqtt3, _mqttConfiguration, new MqttClientCredentials(), true ),
                _waitTimeoutSecs,
                ProtocolConfiguration.Mqtt3,
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
