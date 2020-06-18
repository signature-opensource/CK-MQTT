using CK.MQTT.Common;

namespace CK.MQTT.Abstractions.Packets
{
    public interface IOutgoingPacketWithId : IOutgoingPacket
    {
        int PacketId { get; set; }

        QualityOfService Qos { get; }
    }
}
