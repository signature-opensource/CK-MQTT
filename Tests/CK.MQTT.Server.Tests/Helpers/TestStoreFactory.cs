using CK.MQTT.Stores;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server.Tests.Helpers
{
    public class TestStoreFactory : IStoreFactory
    {
        readonly MQTT3ConfigurationBase _config;

        public TestStoreFactory( MQTT3ConfigurationBase _config )
        {
            this._config = _config;
        }
        public ValueTask<(ILocalPacketStore, IRemotePacketStore)> CreateAsync( ProtocolConfiguration pConfig, MQTT3ConfigurationBase config, string storeId, bool resetStore, CancellationToken cancellationToken )
            => new(
                (
                    new MemoryPacketStore( ProtocolConfiguration.MQTT3, _config, ushort.MaxValue ),
                    new MemoryPacketIdStore()
                )
            );
    }
}
