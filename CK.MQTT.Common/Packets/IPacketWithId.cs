namespace CK.MQTT.Common.Packets
{
    public interface IPacketWithId : IPacket
    {
        public ushort PacketId { get; }
    }
}
