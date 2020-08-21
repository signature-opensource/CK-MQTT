namespace CK.MQTT
{
    class OutgoingUnsuscribeAck : OutgoingPacketWithId
    {
        public OutgoingUnsuscribeAck( ushort packetId ) : base( packetId )
        {
        }

        /// <inheritdoc/>
        public override byte Header => (byte)PacketType.UnsubscribeAck;
    }
}
