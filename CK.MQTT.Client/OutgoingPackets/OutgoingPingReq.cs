using CK.MQTT.Common;
using CK.MQTT.Common.Packets;
using System;
using System.IO.Pipelines;

namespace CK.MQTT.Client.OutgoingPackets
{
    class OutgoingPingReq : SimpleOutgoingPacket
    {
        protected override PacketType PacketType => PacketType.PingRequest;

        protected override void Write( PipeWriter pw )
        {
            Span<byte> span = pw.GetSpan( 2 );
            span[0] = (byte)PacketType;
            span[1] = 0;
            pw.Advance( 2 );
        }
    }
}
