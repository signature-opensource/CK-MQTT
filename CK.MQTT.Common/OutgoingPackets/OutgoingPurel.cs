using CK.MQTT.Common.Packets;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.MQTT.Common.OutgoingPackets
{
    public class OutgoingPubrel : OutgoingPacketWithId
    {
        public OutgoingPubrel( ushort packetId ) : base( packetId )
        {
        }

        public override byte Header => (byte)PacketType.PublishRelease;

        protected override PacketType PacketType => PacketType.PublishRelease;
    }
}
