using CK.MQTT.Common;
using CK.MQTT.Common.Packets;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
