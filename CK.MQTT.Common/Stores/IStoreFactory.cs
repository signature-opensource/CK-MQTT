using CK.Core;
using CK.MQTT.Stores;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IStoreFactory
    {
        ValueTask<(ILocalPacketStore, IRemotePacketStore)> CreateAsync( IActivityMonitor? m, ProtocolConfiguration pConfig, MqttConfigurationBase config, string storeId, bool resetStore );
        ValueTask<(ILocalPacketStore, IRemotePacketStore)> CreateAsync( IInputLogger? m, ProtocolConfiguration pConfig, MqttConfigurationBase config, string storeId, bool resetStore );
    }
}
