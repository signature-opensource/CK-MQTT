using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IStoreFactory
    {
        ValueTask<(PacketStore, IPacketIdStore)> CreateAsync( IMqttLogger m, IStoreTransformer storeTransformer, string storeId, bool resetStore );
    }
}
