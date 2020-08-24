namespace CK.MQTT
{
    class OutgoingUnsuscribeAck
    {
        public static IOutgoingPacketWithId UnsuscribeAck( int packetId ) => new LifecyclePacketV3( (byte)PacketType.UnsubscribeAck, packetId );
    }
}
