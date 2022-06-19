using CK.MQTT.Packets;
using System;

namespace CK.MQTT
{
    public interface IStoreTransformer
    {
        Func<IOutgoingPacket, IOutgoingPacket> PacketTransformerOnRestore { get; }
        Func<IOutgoingPacket, IOutgoingPacket> PacketTransformerOnSave { get; }
    }
}
