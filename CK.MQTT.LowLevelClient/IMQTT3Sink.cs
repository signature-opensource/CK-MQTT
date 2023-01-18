using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public interface IMQTT3Sink
    {
        IConnectedMessageSender Sender { get; set; }
        ValueTask OnMessageAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken );

        void OnUnattendedDisconnect( DisconnectReason reason );
        void OnUserDisconnect( bool clearSession );

        void OnPacketResent( ushort packetId, ulong packetInTransitOrLost, bool isDropped );

        void OnQueueFullPacketDropped( ushort packetId );
        void OnQueueFullPacketDropped( ushort packetId, PacketType packetType );

        void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData );
        void OnPacketWithDupFlagReceived( PacketType packetType );
    }
}
