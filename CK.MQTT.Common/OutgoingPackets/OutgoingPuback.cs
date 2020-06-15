using CK.MQTT.Common.Packets;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.MQTT.Common.OutgoingPackets
{
    public class OutgoingPuback : OutgoingPacketWithId
    {
        public OutgoingPuback( ushort packetId ) : base( packetId )
        {
        }

        public override byte Header => (byte)PacketType.PublishAck;

        protected override PacketType PacketType => PacketType.PublishAck;
    }
}
