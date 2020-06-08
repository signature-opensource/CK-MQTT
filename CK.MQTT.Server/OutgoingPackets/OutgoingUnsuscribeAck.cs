using CK.MQTT.Common;
using CK.MQTT.Common.OutgoingPackets;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;

namespace CK.MQTT.Server.OutgoingPackets
{
    class OutgoingUnsuscribeAck : OutgoingPacketWithId
    {
        public OutgoingUnsuscribeAck( ushort packetId ) : base( packetId )
        {
        }

        public override byte Header => (byte)PacketType;

        protected override PacketType PacketType => PacketType.UnsubscribeAck;
    }
}
