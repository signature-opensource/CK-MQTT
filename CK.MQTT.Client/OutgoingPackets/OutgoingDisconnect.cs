using CK.MQTT.Common.Packets;
using System;

namespace CK.MQTT.Common
{
    public class OutgoingDisconnect : SimpleOutgoingPacket
    {
        public override int Size => 2;

        protected override void Write( Span<byte> span )
        {
            span[0] = (byte)PacketType.Disconnect;
            span[1] = 0;
        }
    }
}
