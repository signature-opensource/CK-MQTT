using CK.MQTT;
using CK.MQTT.Stores;

namespace CK.LogHub
{
    public class TodoStoreFactory : IStoreFactory
    {
        readonly Mqtt3ConfigurationBase _config;

        public TodoStoreFactory(Mqtt3ConfigurationBase _config)
        {
            this._config = _config;
        }
        public ValueTask<(ILocalPacketStore, IRemotePacketStore)> CreateAsync( ProtocolConfiguration pConfig, Mqtt3ConfigurationBase config, string storeId, bool resetStore, CancellationToken cancellationToken )
            => new ValueTask<(ILocalPacketStore, IRemotePacketStore)>(
                (
                    new MemoryPacketStore( ProtocolConfiguration.Mqtt3, _config, ushort.MaxValue ),
                    new MemoryPacketIdStore()
                )
            );
    }
}
