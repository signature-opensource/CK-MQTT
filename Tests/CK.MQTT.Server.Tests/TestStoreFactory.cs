using CK.MQTT;
using CK.MQTT.Stores;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server.Tests
{
    public class TestStoreFactory : IStoreFactory
    {
        readonly Mqtt3ConfigurationBase _config;

        public TestStoreFactory(Mqtt3ConfigurationBase _config)
        {
            this._config = _config;
        }
        public ValueTask<(ILocalPacketStore, IRemotePacketStore)> CreateAsync( ProtocolConfiguration pConfig, Mqtt3ConfigurationBase config, string storeId, bool resetStore, CancellationToken cancellationToken )
            => new(
                (
                    new MemoryPacketStore( ProtocolConfiguration.Mqtt3, _config, ushort.MaxValue ),
                    new MemoryPacketIdStore()
                )
            );
    }
}
