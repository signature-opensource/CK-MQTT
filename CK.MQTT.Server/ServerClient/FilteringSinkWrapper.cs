using CK.MQTT.Client;
using CK.MQTT.P2P;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
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
}
