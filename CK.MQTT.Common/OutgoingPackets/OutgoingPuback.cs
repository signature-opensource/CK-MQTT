namespace CK.MQTT.Common
{
    public class OutgoingPuback : OutgoingPacketWithId
    {
        public OutgoingPuback( ushort packetId ) : base( packetId )
        {
        }

        public override byte Header => (byte)PacketType.PublishAck;
    }
}
