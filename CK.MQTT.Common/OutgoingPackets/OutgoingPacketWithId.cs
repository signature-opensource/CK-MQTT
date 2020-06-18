using CK.MQTT.Abstractions.Packets;
using CK.MQTT.Common.Serialisation;
using System;
using System.IO.Pipelines;

namespace CK.MQTT.Common.OutgoingPackets
{
    public abstract class OutgoingPacketWithId : SimpleOutgoingPacket, IOutgoingPacketWithId
    {
        public abstract byte Header { get; }
        public int PacketId { get; set; }

        protected OutgoingPacketWithId( ushort packetId )
        {
            PacketId = packetId;
        }

        protected override void Write( PipeWriter pw )
        {
            Span<byte> span = pw.GetSpan( 4 );
            span[0] = Header;
            span[1] = 2;
            span[2..].WriteUInt16( (ushort)PacketId );
        }
    }
}
