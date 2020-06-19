using CK.MQTT.Common.Channels;

namespace CK.MQTT.Common
{
    public interface IMqttChannelFactory
    {
        IMqttChannel Create( string connectionString );
    }
}
