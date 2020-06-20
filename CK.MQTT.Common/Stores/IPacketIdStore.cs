using CK.Core;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Stores
{
    public interface IPacketIdStore
    {
        ValueTask StoreId( IActivityMonitor m, int id );

        ValueTask RemoveId( IActivityMonitor m, int id );
    }
}
