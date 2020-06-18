using CK.Core;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Stores
{
    public class VolatilePacketStoreManager : IPacketStoreManager
    {
        public Task<PacketStore> CreateAsync( IActivityMonitor m, string storeId, bool cleanSession )
            => Task.FromResult<PacketStore>( new MemoryPacketStore() );

        public Task<bool> DeleteAsync( IActivityMonitor m, string storeId ) => Task.FromResult( true );
    }
}
