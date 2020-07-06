namespace CK.MQTT
{
    public class OutgoingPubrel : OutgoingPacketWithId
    {
        public OutgoingPubrel( ushort packetId ) : base( packetId )
        {
        }

        public override byte Header => (byte)PacketType.PublishRelease | 0b0010; //bit set due to: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc384800426
    }
}
