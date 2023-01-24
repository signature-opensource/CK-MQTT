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
            await await client.PublishAsync( topic: "test", new byte[256000000], qos: QualityOfService.AtLeastOnce, retain: false );
            await Task.Delay( 5000 );
        }
    }
}
