using static CK.MQTT.PacketStore;

namespace CK.MQTT
{
    public interface IStoreTransformer
    {
        PacketTransformer PacketTransformerOnRestore { get; }
        PacketTransformer PacketTransformerOnSave { get; }
    }
}
