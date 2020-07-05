using CK.Core;
using System.Threading.Tasks;

namespace CK.MQTT.Common
{
    public interface IPacketStoreManager
    {
        Task<PacketStore> CreateAsync( IActivityMonitor m, string storeId, bool cleanSession );

        Task<bool> DeleteAsync( IActivityMonitor m, string storeId );
    }
}
