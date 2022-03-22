namespace CK.MQTT.Packets
{
    class OutgoingUnsuscribeAck
    {
        public static IOutgoingPacket UnsuscribeAck( ushort packetId ) => new LifecyclePacketV3( (byte)PacketType.UnsubscribeAck, packetId, true);
    }
}
