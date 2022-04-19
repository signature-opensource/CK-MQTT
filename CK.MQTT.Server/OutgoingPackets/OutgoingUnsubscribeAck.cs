namespace CK.MQTT.Packets
{
    class OutgoingUnsubscribeAck
    {
        public static IOutgoingPacket UnsubscribeAck( ushort packetId ) => new LifecyclePacketV3( (byte)PacketType.UnsubscribeAck, packetId, true);
    }
}
