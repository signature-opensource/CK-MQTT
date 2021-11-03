using CK.Core;
using CK.MQTT.Stores;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class MemoryStoreFactory : IStoreFactory
    {
        readonly Dictionary<string, (IOutgoingPacketStore, IIncomingPacketStore)> _stores = new();
        public ValueTask<(IOutgoingPacketStore, IIncomingPacketStore)> CreateAsync( IActivityMonitor? m, ProtocolConfiguration pConfig, MqttConfigurationBase config, string storeId, bool resetStore )
        {
            bool newSession = resetStore || !_stores.ContainsKey( storeId );
            if( newSession )
            {
                _stores[storeId] = (new MemoryPacketStore( pConfig, config, ushort.MaxValue ), new MemoryPacketIdStore());
            }
            var currStore = _stores[storeId];
            currStore.Item1.IsRevivedSession = !newSession;
            currStore.Item2.IsRevivedSession = !newSession;
            return new ValueTask<(IOutgoingPacketStore, IIncomingPacketStore)>( currStore );
        }

        public ValueTask<(IOutgoingPacketStore, IIncomingPacketStore)> CreateAsync(
            IInputLogger? m, ProtocolConfiguration pConfig, MqttConfigurationBase config, string storeId,
            bool resetStore )
        {
            bool newSession = resetStore || !_stores.ContainsKey( storeId );
            if( newSession )
            {
                _stores[storeId] = (new MemoryPacketStore( pConfig, config, ushort.MaxValue ), new MemoryPacketIdStore());
            }
            var currStore = _stores[storeId];
            currStore.Item1.IsRevivedSession = !newSession;
            currStore.Item2.IsRevivedSession = !newSession;
            return new ValueTask<(IOutgoingPacketStore, IIncomingPacketStore)>( currStore );
        }
    }
}
