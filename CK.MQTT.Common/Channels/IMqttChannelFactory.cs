using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IMqttChannelFactory
    {
        ValueTask<IMqttChannel> CreateAsync( IMqttLogger m, string connectionString );
    }
}
