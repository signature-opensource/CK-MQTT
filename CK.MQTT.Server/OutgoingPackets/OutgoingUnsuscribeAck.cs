namespace CK.MQTT
{
    class OutgoingUnsuscribeAck
    {
        public static IOutgoingPacket UnsuscribeAck( ushort PacketId ) => new LifecyclePacketV3( (byte)PacketType.UnsubscribeAck, packetId, true);
    }
}
