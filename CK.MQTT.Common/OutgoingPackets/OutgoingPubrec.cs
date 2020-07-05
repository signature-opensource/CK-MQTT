namespace CK.MQTT.Common
{
    public class OutgoingPubrec : OutgoingPacketWithId
    {
        public OutgoingPubrec( ushort packetId ) : base( packetId )
        {
        }

        public override byte Header => (byte)PacketType.PublishReceived;
    }
}
