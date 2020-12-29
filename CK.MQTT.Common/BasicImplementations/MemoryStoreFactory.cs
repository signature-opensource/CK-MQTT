using CK.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class MemoryStoreFactory : IStoreFactory
    {
        readonly Dictionary<string, (PacketStore, IPacketIdStore)> _stores = new Dictionary<string, (PacketStore, IPacketIdStore)>();
        public ValueTask<(PacketStore, IPacketIdStore)> CreateAsync( IActivityMonitor? m, ProtocolConfiguration pConfig, MqttConfigurationBase config, string storeId, bool resetStore )
        {
            if( resetStore )
            {
                _stores[storeId] = (new MemoryPacketStore(pConfig, config, ushort.MaxValue ), new MemoryPacketIdStore());
            }
            return new ValueTask<(PacketStore, IPacketIdStore)>( _stores[storeId] );
        }
    }
}
