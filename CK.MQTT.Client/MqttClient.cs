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
using CK.MQTT.Common.Processes;

namespace CK.MQTT.Client
{
    class MqttClient : IMqttClient
    {
        bool _channelWasConnected;
        readonly IPacketStore _store;
        readonly MqttConfiguration _config;
        readonly IMqttChannel _channel;
        readonly OutgoingMessageHandler _output;
        readonly IncomingMessageHandler _input;
        readonly int _waitTimeoutSecs;
        Reflex? _postConnectReflex;
        MqttClient(
            IPacketStore store,
            MqttConfiguration mqttConfiguration,
            IMqttChannel channel,
            IncomingMessageHandler incomingHandler,
            OutgoingMessageHandler outgoingHandler,
            int waitTimeoutSecs,
            string clientId )
        {
            _store = store;
            _config = mqttConfiguration;
            ClientId = clientId;
            _channel = channel;
            _output = outgoingHandler;
            _input = incomingHandler;
            _waitTimeoutSecs = waitTimeoutSecs;
        }
        public static IMqttClient Create( IPacketStore store,
            MqttConfiguration mqttConfiguration,
            IMqttChannel channel,
            PipeWriter pipeWriter,
            Channel<OutgoingPacket> externalMessageChannel,
            Channel<OutgoingPacket> internalMessageChannel,
            IncomingMessageHandler incomingHandler,
            int waitTimeoutSecs,
            string clientId = "" )
        {
            ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder();//I think there is a code smell there, object referencing each other make the instantiation complex.
            ushort keepAlive = mqttConfiguration.KeepAliveSecs;
            bool useKeepAlive = keepAlive != 0;
            KeepAliveTimer? timer = useKeepAlive ? new KeepAliveTimer( TimeSpan.FromSeconds( keepAlive ), TimeSpan.FromSeconds( mqttConfiguration.WaitTimeoutSecs ) ) : null;
            var output = new OutgoingMessageHandler( pipeWriter, useKeepAlive ? timer!.OutputTransformer : (OutgoingMessageHandler.OutputTransformer?)null, externalMessageChannel, internalMessageChannel );
            if( useKeepAlive ) timer.OutgoingMessageHandler = output;
            var client = new MqttClient( store, mqttConfiguration, channel, incomingHandler, output, waitTimeoutSecs, clientId );
            if( useKeepAlive ) timer.TimeoutCallback = client.PingReqTimeout;
            builder.UseMiddleware( new PublishReflex( store, client.RaiseMessage, output ) );
            builder.UseMiddleware( new PubackReflex( store ) );
            builder.UseMiddleware( new PubReceivedReflex( store, output ) );
            builder.UseMiddleware( new PubRelReflex( store, output ) );
            builder.UseMiddleware( new SubackReflex( client.OnSuback ) );
            builder.UseMiddleware( new UnsubackReflex( client.OnUnsuback ) );
            builder.UseMiddleware( new PingRespReflex( useKeepAlive ? timer.ResetTimer : (Action?)null ) );
            var postConnectReflex = builder.Build( client.InvalidPacket );
            return client;
        }

        Dictionary<ushort, TaskCompletionSource<QualityOfService[]>> _subackTasks = new Dictionary<ushort, TaskCompletionSource<QualityOfService[]>>();
        void OnSuback( ushort packetId, QualityOfService[] qos )
        {

        }

        Dictionary<ushort, TaskCompletionSource<object>> _unsubackTask = new Dictionary<ushort, TaskCompletionSource<object>>();
        void OnUnsuback( ushort packetId )
        {

        }

        void PingReqTimeout( IActivityMonitor m ) => Close( m, DisconnectedReason.RemoteDisconnected );

        Task RaiseMessage( IActivityMonitor m, IncomingApplicationMessage msg ) => _eMessage.RaiseAsync( m, this, msg );

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

        public Task<ConnectResult> ConnectAsync( IActivityMonitor m, MqttClientCredentials? credentials = null, OutgoingLastWill? lastWill = null )
            => ConnectProcess.ExecuteConnectProtocol( m, _output, _input,
                new OutgoingConnect( ProtocolConfiguration.Mqtt3, _config, credentials, lastWill), _config.WaitTimeoutSecs, _postConnectReflex! );


        public async ValueTask DisconnectAsync( IActivityMonitor m )
        {
            await _output.SendMessageAsync( new OutgoingDisconnect() );
        }

        public Task<ValueTask> PublishAsync( IActivityMonitor m, OutgoingApplicationMessage message, string topic, ReadOnlyMemory<byte> payload, QualityOfService qos, bool retain = false )
            => PublishSenderProcesses.Publish()
        }

        public ValueTask<ValueTask<IReadOnlyCollection<SubscribeReturnCode>?>> SubscribeAsync( IActivityMonitor m, params Subscription[] subscriptions )
        {
            throw new NotImplementedException();
        }

        public ValueTask<ValueTask<bool>> UnsubscribeAsync( IActivityMonitor m, params string[] topics )
        {
            throw new NotImplementedException();
        }

        public Task<IncomingApplicationMessage?> WaitMessageReceivedAsync( Func<IncomingApplicationMessage, bool>? predicate = null, int timeoutMillisecond = -1 )
        {
            throw new NotImplementedException();
        }
    }
}
