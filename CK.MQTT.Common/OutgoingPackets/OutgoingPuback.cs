using CK.MQTT.Common.Packets;

namespace CK.MQTT.Common.OutgoingPackets
{
    public class OutgoingPuback : OutgoingPacketWithId
    {
        public OutgoingPuback( ushort packetId ) : base( packetId )
        {
        }

        public override byte Header => (byte)PacketType.PublishAck;
    }
}
