using CK.Core;
using CK.MQTT.Common.Packets;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Channels
{
    public interface IPacketChannelFactory
    {
        Task<IMqttChannel<IPacket>> CreateAsync( IActivityMonitor m, string connectionString );
    }
}
