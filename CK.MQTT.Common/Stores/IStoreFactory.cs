using CK.Core;
using CK.MQTT.Stores;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IStoreFactory
    {
        ValueTask<(IOutgoingPacketStore, IIncomingPacketStore)> CreateAsync( IActivityMonitor? m, ProtocolConfiguration pConfig, MqttConfigurationBase config, string storeId, bool resetStore );
    }
}
