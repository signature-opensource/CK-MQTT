using CK.MQTT.Common.Packets;

namespace CK.MQTT.Common.OutgoingPackets
{
    public class OutgoingPubcomp : OutgoingPacketWithId
    {
        public OutgoingPubcomp( ushort packetId ) : base( packetId )
        {
        }

        public override byte Header => (byte)PacketType.PublishComplete;
    }
}
