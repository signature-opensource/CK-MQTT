using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Stores
{
    public class VolatilePacketStoreManager : IPacketStoreManager
    {
        public Task<IPacketStore> CreateAsync( IActivityMonitor m, string storeId, bool cleanSession )
            => Task.FromResult<IPacketStore>( new MemoryPacketStore() );

        public Task<bool> DeleteAsync( IActivityMonitor m, string storeId ) => Task.FromResult( true );
    }
}
