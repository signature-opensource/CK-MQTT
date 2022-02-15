using CK.Core;
using CK.Monitoring;
using CK.Monitoring.Handlers;
using CK.MQTT;
using CK.MQTT.Client;
using System;
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

            ActivityMonitor m = new();
            var client = MqttClient.Factory.CreateMQTT3Client( new MqttClientConfiguration( "localhost:1883" )
            {
                KeepAliveSeconds = 3,
                WaitTimeoutMilliseconds = 1000
            },
                MessageHandlerDelegate
            );
            client.DisconnectedHandler += ( reason, task ) =>
            {
                System.Console.WriteLine( reason );
            };
            var res = await client.ConnectAsync( m );
            await Task.Delay( 3000000 );
            await client.DisconnectAsync( m, true, true, default );
        }
    }
}
