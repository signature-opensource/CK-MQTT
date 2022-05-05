using CK.MQTT.Packets;

namespace CK.MQTT.Server.OutgoingPackets
{
    class OutgoingUnsubscribeAck
    {
        public static IOutgoingPacket UnsubscribeAck( ushort packetId ) => new LifecyclePacketV3( (byte)PacketType.UnsubscribeAck, packetId, true );
    }
}
