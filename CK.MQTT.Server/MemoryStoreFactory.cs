using CK.MQTT.Stores;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server;

public class MemoryStoreFactory : IStoreFactory
{
    readonly Dictionary<string, (ILocalPacketStore, IRemotePacketStore)> _stores = new();
    public ValueTask<(ILocalPacketStore, IRemotePacketStore)> CreateAsync( ProtocolConfiguration pConfig, MQTT3ConfigurationBase config, string storeId, bool resetStore, CancellationToken cancellationToken )
    {
        bool newSession = resetStore || !_stores.ContainsKey( storeId );
        if( newSession )
        {
            _stores[storeId] = (new MemoryPacketStore( pConfig, config, ushort.MaxValue ), new MemoryPacketIdStore());
        }
        var currStore = _stores[storeId];
        currStore.Item1.IsRevivedSession = !newSession;
        currStore.Item2.IsRevivedSession = !newSession;
        return new ValueTask<(ILocalPacketStore, IRemotePacketStore)>( currStore );
    }
}
