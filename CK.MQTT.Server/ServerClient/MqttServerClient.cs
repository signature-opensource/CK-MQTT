using CK.MQTT.Client;
using CK.MQTT.Common.Pumps;
using CK.MQTT.Packets;
using CK.MQTT.Pumps;
using CK.MQTT.Server;
using CK.MQTT.Stores;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.P2P
{
    public class MqttServerClient : MessageExchanger, IMqtt3Client
    {
        readonly ITopicFilter _topicFilter = new TopicFilter();
        readonly Channel<(bool, string[])> _subscriptionsCommand = System.Threading.Channels.Channel.CreateUnbounded<(bool, string[])>();
        readonly ISecurityManager _securityManager;

        internal MqttServerClient(
            ProtocolConfiguration pConfig,
            MqttServerClientConfiguration config,
            IMqtt3Sink sink,
            IMqttChannel channel,
            ISecurityManager securityManager,
            IRemotePacketStore? remotePacketStore = null,
            ILocalPacketStore? localPacketStore = null
        )
            : base( pConfig, config, sink, channel, remotePacketStore, localPacketStore )
        {
            P2PConfig = config;
            _securityManager = securityManager;
            Sink = new SinkWrapper( sink, _topicFilter );
        }

        class SinkWrapper : IMqtt3Sink
        {
            readonly IMqtt3Sink _sink;
            readonly ITopicFilter _topicFilter;
            public SinkWrapper( IMqtt3Sink sink, ITopicFilter topicFilter )
            {
                _sink = sink;
                _topicFilter = topicFilter;
            }

            public void Connected() => _sink.Connected();

            public void OnPacketResent( ushort packetId, int packetInTransitOrLost, bool isDropped ) => _sink.OnPacketResent( packetId, packetInTransitOrLost, isDropped );

            public void OnPacketWithDupFlagReceived( PacketType packetType ) => _sink.OnPacketWithDupFlagReceived( packetType );

            public void OnPoisonousPacket( ushort packetId, PacketType packetType, int poisonousTotalCount ) => _sink.OnPoisonousPacket( packetId, packetType, poisonousTotalCount );

            public void OnQueueFullPacketDropped( ushort packetId, PacketType packetType ) => _sink.OnQueueFullPacketDropped( packetId, packetType );

            public bool OnReconnectionFailed( int retryCount, int maxRetryCount ) => _sink.OnReconnectionFailed( retryCount, maxRetryCount );

            public void OnStoreFull( ushort freeLeftSlot ) => _sink.OnStoreFull( freeLeftSlot );

            public void OnUnattendedDisconnect( DisconnectReason reason ) => _sink.OnUnattendedDisconnect( reason );

            public void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData ) => _sink.OnUnparsedExtraData( packetId, unparsedData );

            public async ValueTask ReceiveAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken )
            {
                if( _topicFilter.IsFiltered( topic ) )
                {
                    await reader.SkipBytesAsync( null, 0, size, cancellationToken );
                }
                else
                {
                    await _sink.ReceiveAsync( topic, reader, size, q, retain, cancellationToken );
                }
            }
        }

        class ClientWrapper: 
        public MqttServerClientConfiguration P2PConfig { get; }

        public async Task<ConnectResult> ConnectAsync( OutgoingLastWill? lastWill = null, CancellationToken cancellationToken = default )
        {


                


                
                var outputProcessor = new FiltringOutputProcessor( _topicFilter, PConfig, output, Channel.DuplexPipe.Output, LocalPacketStore );
                // Enable keepalive only if we need it.

               
                bool hasExistingSession = connectReflex.OutStore.IsRevivedSession || connectReflex.InStore.IsRevivedSession;
            }
            
        }


        public async ValueTask<Task<SubscribeReturnCode>> SubscribeAsync( Subscription subscriptions )
        {
            await _subscriptionsCommand.Writer.WriteAsync( (true, new string[] { subscriptions.TopicFilter }) );
            return Task.FromResult(
                SubscribeReturnCode.MaximumQoS0
            // We cannot ask the client a certain QoS as it's a fake subscribe, we return 0 because we canno't make the guarentee the QoS will be higher.
            );
        }

        public async ValueTask<Task<SubscribeReturnCode[]>> SubscribeAsync( IEnumerable<Subscription> subscriptions )
        {
            var subs = subscriptions.Select( s => s.TopicFilter ).ToArray();
            await _subscriptionsCommand.Writer.WriteAsync( (true, subs) );
            return Task.FromResult(
                new SubscribeReturnCode[subs.Length]
            // We cannot ask the client a certain QoS as it's a fake subscribe, we return 0 because we canno't make the guarentee the QoS will be higher.
            );
        }

        public async ValueTask<Task> UnsubscribeAsync( params string[] topics )
        {
            await _subscriptionsCommand.Writer.WriteAsync( (false, topics) );
            return Task.CompletedTask;
        }
    }
}
