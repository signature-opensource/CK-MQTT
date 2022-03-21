using CK.MQTT.Stores;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IStoreFactory
    {
        ValueTask<(ILocalPacketStore, IRemotePacketStore)> CreateAsync( ProtocolConfiguration pConfig, MqttConfigurationBase config, string storeId, bool resetStore );
    }
}
