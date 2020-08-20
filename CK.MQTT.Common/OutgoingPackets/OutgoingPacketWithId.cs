using System;

namespace CK.MQTT
{
    public abstract class OutgoingPacketWithId : SimpleOutgoingPacket, IOutgoingPacketWithId
    {
        public abstract byte Header { get; }

        public int PacketId { get; set; }

        public QualityOfService Qos => QualityOfService.AtLeastOnce;

        protected OutgoingPacketWithId( ushort packetId )
        {
            PacketId = packetId;
        }

        /// <inheritdoc/>
        public override int Size => 4;

        /// <inheritdoc/>
        protected override void Write( Span<byte> span )
        {
            span[0] = Header;
            span[1] = 2;
            span[2..].WriteUInt16( (ushort)PacketId );
        }
    }
}
