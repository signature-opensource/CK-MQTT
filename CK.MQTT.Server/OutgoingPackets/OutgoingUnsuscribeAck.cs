namespace CK.MQTT
{
    class OutgoingUnsuscribeAck
    {
        public static IOutgoingPacket UnsuscribeAck( int packetId ) => new LifecyclePacketV3( (byte)PacketType.UnsubscribeAck, packetId, true );
    }
}
