using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public interface IMqtt3Sink
    {
        IConnectedMessageSender Sender { get; set; }
        ValueTask OnMessageAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken );

        /// <returns><see langword="true"/>to keep reconnecting.</returns>
        bool OnUnattendedDisconnect( DisconnectReason reason );

        void OnPacketResent( ushort packetId, ulong packetInTransitOrLost, bool isDropped );

        void OnQueueFullPacketDropped( ushort packetId );
        void OnQueueFullPacketDropped( ushort packetId, PacketType packetType );

        void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData );
        void OnPacketWithDupFlagReceived( PacketType packetType );
    }
}
