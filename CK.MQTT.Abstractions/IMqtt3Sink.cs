using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public interface IMqtt3Sink
    {
        ValueTask ReceiveAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken );

        void OnUnattendedDisconnect( DisconnectReason reason );

        bool OnReconnectionFailed( int retryCount, int maxRetryCount );

        void Connected();

        void OnStoreFull( ushort freeLeftSlot );

        void OnPoisonousPacket( ushort packetId, PacketType packetType, int poisonousTotalCount );

        void OnPacketResent( ushort packetId, int packetInTransitOrLost, bool isDropped );

        void OnQueueFullPacketDropped( ushort packetId, PacketType packetType );

        void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData );
        void OnPacketWithDupFlagReceived( PacketType packetType );
    }
}
