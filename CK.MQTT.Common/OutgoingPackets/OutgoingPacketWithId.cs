using CK.MQTT.Abstractions.Packets;
using CK.MQTT.Common.Serialisation;
using System;

namespace CK.MQTT.Common.OutgoingPackets
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

        public override int Size => 4;

        protected override void Write( Span<byte> span )
        {
            span[0] = Header;
            span[1] = 2;
            span[2..].WriteUInt16( (ushort)PacketId );
        }
    }
}
