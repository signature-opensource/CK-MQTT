using CK.Core;
using CK.Monitoring;
using CK.Monitoring.Handlers;
using CK.MQTT;
using CK.MQTT.Client;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#nullable enable

namespace SimpleClientTest
{
    class Program
    {
        static ValueTask MessageHandlerDelegate( IActivityMonitor? m, ApplicationMessage msg, CancellationToken cancellationToken )
        {
            System.Console.WriteLine( msg.Topic + Encoding.UTF8.GetString( msg.Payload.Span ) );
            return new();
        }
        static async Task Main()
        {
            //test.mosquitto.org
            var cfg = new GrandOutputConfiguration();
            cfg.Handlers.Add( new ConsoleConfiguration() );
            GrandOutput.EnsureActiveDefault( cfg );
            ActivityMonitor m = new();
            var client = MqttClient.Factory.CreateMQTT3Client( new MqttClientConfiguration( "localhost:1883" ),
                MessageHandlerDelegate
            );
            var res = await client.ConnectAsync( m );
            while( true )
            {
                string line = System.Console.ReadLine();
                await client.PublishAsync( m, new ApplicationMessage( "foo", Encoding.UTF8.GetBytes( line ), QualityOfService.AtMostOnce, false ) );
            }
            await client.DisconnectAsync( m, true, true, default );
        }
    }
}
