using CK.Core;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    class ClientInstance
    {
        readonly MqttServer _mqttServer;
        readonly IMqttChannel _channel;

        ClientInstance( MqttServer mqttServer, IMqttChannel channel )
        {
            _mqttServer = mqttServer;
            _channel = channel;
        }
        public static ClientInstance HandleIncomingConnection( IActivityMonitor m, MqttServer server, IMqttChannel channel )
        {
            ClientInstance instance = new( server, channel );

            return instance;
        }

        async Task HandleConnection()
        {
            _channel.DuplexPipe.Input.ReadAsync
        }
    }
}
