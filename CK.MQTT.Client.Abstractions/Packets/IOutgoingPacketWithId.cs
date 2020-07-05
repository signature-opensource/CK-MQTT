namespace CK.MQTT
{
    public interface IOutgoingPacketWithId : IOutgoingPacket
    {
        int PacketId { get; set; }

        QualityOfService Qos { get; }
    }
}
