using CK.Core;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IStoreFactory
    {
        ValueTask<(IPacketStore, IPacketIdStore)> CreateAsync( IActivityMonitor? m, ProtocolConfiguration pConfig, MqttConfigurationBase config, string storeId, bool resetStore );
    }
}
