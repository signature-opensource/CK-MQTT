using CK.MQTT.Common.OutgoingPackets;
using CK.MQTT.Common.Packets;

namespace CK.MQTT.Server.OutgoingPackets
{
    class OutgoingUnsuscribeAck : OutgoingPacketWithId
    {
        public OutgoingUnsuscribeAck( ushort packetId ) : base( packetId )
        {
        }

        public override byte Header => (byte)PacketType.UnsubscribeAck;
    }
}
