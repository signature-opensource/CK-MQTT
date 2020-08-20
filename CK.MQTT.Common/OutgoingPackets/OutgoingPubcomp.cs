namespace CK.MQTT
{
    public class OutgoingPubcomp : OutgoingPacketWithId
    {
        public OutgoingPubcomp( ushort packetId ) : base( packetId )
        {
        }

        /// <inheritdoc/>
        public override byte Header => (byte)PacketType.PublishComplete;
    }
}
