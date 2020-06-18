using CK.MQTT.Common.Packets;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.MQTT.Common.OutgoingPackets
{
    public class OutgoingPubrec : OutgoingPacketWithId
    {
        public OutgoingPubrec( ushort packetId ) : base( packetId )
        {
        }

        public override byte Header => (byte)PacketType.PublishReceived;

        public override QualityOfService Qos => QualityOfService.AtLeastOnce;
    }
}
