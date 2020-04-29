using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Stores
{
    public interface IPacketStoreManager
    {
        Task<IPacketStore> CreateAsync( IActivityMonitor m, string storeId, bool cleanSession );

        Task<bool> DeleteAsync( IActivityMonitor m, string storeId );
    }
}
