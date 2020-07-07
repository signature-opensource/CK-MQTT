using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class MemoryStoreFactory : IStoreFactory
    {
        Dictionary<string, (PacketStore, IPacketIdStore)> _stores = new Dictionary<string, (PacketStore, IPacketIdStore)>();
        public ValueTask<(PacketStore, IPacketIdStore)> CreateAsync( IMqttLogger m, IStoreTransformer storeTransformer, string storeId, bool resetStore )
        {
            if( resetStore )
            {
                _stores[storeId] = (new MemoryPacketStore( storeTransformer, ushort.MaxValue ), new MemoryPacketIdStore());
            }
            return new ValueTask<(PacketStore, IPacketIdStore)>( _stores[storeId] );
        }
    }
}
