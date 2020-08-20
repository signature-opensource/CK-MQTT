namespace CK.MQTT
{
    public class OutgoingPubrec : OutgoingPacketWithId
    {
        public OutgoingPubrec( ushort packetId ) : base( packetId )
        {
        }

        /// <inheritdoc/>
        public override byte Header => (byte)PacketType.PublishReceived;
    }
}
