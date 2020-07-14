using System;
using static CK.MQTT.PacketStore;

namespace CK.MQTT
{
    public interface IStoreTransformer
    {
        Func<IOutgoingPacketWithId, IOutgoingPacketWithId> PacketTransformerOnRestore { get; }
        Func<IOutgoingPacketWithId, IOutgoingPacketWithId> PacketTransformerOnSave { get; }
    }
}
