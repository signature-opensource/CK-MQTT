namespace CK.MQTT
{
    class OutgoingUnsuscribeAck
    {
        public static IOutgoingPacket UnsuscribeAck( uint packetId ) => new LifecyclePacketV3( (byte)PacketType.UnsubscribeAck, packetId );
    }
}
