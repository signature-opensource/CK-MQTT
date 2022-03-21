using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public interface IMqtt3Sink
    {
        ValueTask ReceiveAsync( string topic, uint size, PipeReader reader, bool retain, QualityOfService q );

        void OnUnattendedDisconnect( DisconnectReason reason );

        bool OnReconnectionFailed( int retryCount, int maxRetryCount );

        void OnReconnect();

        void OnStoreFull( ushort freeLeftSlot );

        void OnPoisonousPacket( ushort packetId, PacketType packetType, int poisonousTotalCount );

        void OnPacketResent( ushort packetId, int packetInTransitOrLost, bool isDropped );

        void OnQueueFullPacketDropped( ushort packetId, PacketType packetType );

        void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData );
    }
}
