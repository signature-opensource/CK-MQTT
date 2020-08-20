using CK.Core;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IMqttChannelFactory
    {
        ValueTask<IMqttChannel> CreateAsync( IActivityMonitor m, string connectionString );
    }
}
