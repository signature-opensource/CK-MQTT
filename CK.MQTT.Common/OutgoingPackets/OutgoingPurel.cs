using CK.MQTT.Common.Packets;

namespace CK.MQTT.Common.OutgoingPackets
{
    public class OutgoingPubrel : OutgoingPacketWithId
    {
        public OutgoingPubrel( ushort packetId ) : base( packetId )
        {
        }

        public override byte Header => (byte)PacketType.PublishRelease;
    }
}
