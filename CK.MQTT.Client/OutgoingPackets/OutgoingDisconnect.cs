using CK.MQTT.Common.Packets;
using System;
using System.IO.Pipelines;

namespace CK.MQTT.Common
{
    public class OutgoingDisconnect : SimpleOutgoingPacket
    {
        protected override PacketType PacketType => PacketType.Disconnect;

        protected override void Write( PipeWriter pw )
        {
            Span<byte> span = pw.GetSpan( 2 );
            span[0] = (byte)PacketType;
            span[1] = 0;
            pw.Advance( 2 );
        }
    }
}
