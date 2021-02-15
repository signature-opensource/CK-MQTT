using CK.Core;
using CK.MQTT.Stores;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class MemoryStoreFactory : IStoreFactory
    {
        readonly Dictionary<string, (IMqttIdStore, IPacketIdStore)> _stores = new Dictionary<string, (IMqttIdStore, IPacketIdStore)>();
        public ValueTask<(IMqttIdStore, IPacketIdStore)> CreateAsync( IActivityMonitor? m, ProtocolConfiguration pConfig, MqttConfigurationBase config, string storeId, bool resetStore )
        {
            if( resetStore || _stores[storeId].Item1 == null )
            {
                _stores[storeId] = (new MemoryPacketStore(pConfig, config, ushort.MaxValue ), new MemoryPacketIdStore());
            }
            return new ValueTask<(IMqttIdStore, IPacketIdStore)>( _stores[storeId] );
        }
    }
}
