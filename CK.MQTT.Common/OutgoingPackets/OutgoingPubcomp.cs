namespace CK.MQTT.Common
{
    public class OutgoingPubcomp : OutgoingPacketWithId
    {
        public OutgoingPubcomp( ushort packetId ) : base( packetId )
        {
        }

        public override byte Header => (byte)PacketType.PublishComplete;
    }
}
