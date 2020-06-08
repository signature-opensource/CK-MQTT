using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;

namespace CK.MQTT.Common.OutgoingPackets
{
    public abstract class OutgoingPacketWithId : SimpleOutgoingPacket
    {
        public abstract byte Header { get; }
        public ushort PacketId { get; }

        protected OutgoingPacketWithId( ushort packetId )
        {
            PacketId = packetId;
        }

        protected override void Write( PipeWriter pw )
        {
            Span<byte> span = pw.GetSpan( 4 );
            span[0] = Header;
            span[1] = 2;
            span[2..].WriteUInt16( PacketId );
        }
    }
}
