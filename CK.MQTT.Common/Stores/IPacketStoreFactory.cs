using CK.MQTT.Common.Stores;
using System.Threading.Tasks;

namespace CK.MQTT.Common
{
    public interface IPacketStoreFactory
    {
        Task<PacketStore> CreateAsync( IMqttLogger m, IStoreTransformer storeTransformer, string storeId, bool cleanSession );
    }
}
