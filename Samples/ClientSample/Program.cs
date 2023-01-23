using CK.MQTT;
using CK.MQTT.Client;

namespace ClientSample
{
    public class Program
    {
        public static async Task Main( string[] args )
        {
            var client = MQTTClient.Factory.Build();
            await client.ConnectAsync( cleanSession: true );
            await client.PublishAsync( topic: "test", payload: Array.Empty<byte>(), qos: QualityOfService.AtLeastOnce, retain: false );
        }
    }
}
