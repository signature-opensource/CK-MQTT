using CK.MQTT.Stores;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IStoreFactory
    {
        ValueTask<(ILocalPacketStore, IRemotePacketStore)> CreateAsync( ProtocolConfiguration pConfig, Mqtt3ConfigurationBase config, string storeId, bool resetStore, CancellationToken cancellationToken );
    }
}
