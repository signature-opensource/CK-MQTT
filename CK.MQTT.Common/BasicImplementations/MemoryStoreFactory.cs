using CK.Core;
using CK.MQTT.Stores;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class MemoryStoreFactory : IStoreFactory
    {
        readonly Dictionary<string, (IOutgoingPacketStore, IIncomingPacketStore)> _stores = new Dictionary<string, (IOutgoingPacketStore, IIncomingPacketStore)>();
        public ValueTask<(IOutgoingPacketStore, IIncomingPacketStore)> CreateAsync( IActivityMonitor? m, ProtocolConfiguration pConfig, MqttConfigurationBase config, string storeId, bool resetStore )
        {
            if( resetStore || _stores[storeId].Item1 == null )
            {
                _stores[storeId] = (new MemoryPacketStore(pConfig, config, ushort.MaxValue ), new MemoryPacketIdStore());
            }
            return new ValueTask<(IOutgoingPacketStore, IIncomingPacketStore)>( _stores[storeId] );
        }
    }
}
