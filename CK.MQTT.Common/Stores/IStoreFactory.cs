using CK.Core;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IStoreFactory
    {
        ValueTask<(PacketStore, IPacketIdStore)> CreateAsync( IActivityMonitor m, MqttConfiguration config, string storeId, bool resetStore );
    }
}
