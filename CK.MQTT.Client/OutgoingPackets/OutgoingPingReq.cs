using CK.MQTT.Common;
using CK.MQTT.Common.Packets;
using System;
using System.IO.Pipelines;

namespace CK.MQTT.Client.OutgoingPackets
{
    class OutgoingPingReq : SimpleOutgoingPacket
    {
        public override int GetSize() => 2;

        protected override void Write( PipeWriter pw )
        {
            Span<byte> span = pw.GetSpan( 2 );
            span[0] = (byte)PacketType.PingRequest;
            span[1] = 0;
            pw.Advance( 2 );
        }
    }
}
