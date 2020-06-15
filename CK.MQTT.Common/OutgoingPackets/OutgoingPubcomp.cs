using CK.MQTT.Common.Packets;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.MQTT.Common.OutgoingPackets
{
    public class OutgoingPubcomp : OutgoingPacketWithId
    {
        public OutgoingPubcomp( ushort packetId ) : base( packetId )
        {
        }

        public override byte Header => (byte)PacketType.PublishComplete;

        protected override PacketType PacketType => PacketType.PublishComplete;
    }
}
