using CK.MQTT.Common;
using CK.MQTT.Common.Packets;
using System;

namespace CK.MQTT.Client.OutgoingPackets
{
    class OutgoingPingReq : SimpleOutgoingPacket
    {
        public override int Size => 2;

        protected override void Write( Span<byte> span )
        {
            span[0] = (byte)PacketType.PingRequest;
            span[1] = 0;
        }
    }
}
