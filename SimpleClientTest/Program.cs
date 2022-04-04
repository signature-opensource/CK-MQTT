using CK.Core;
using CK.Monitoring;
using CK.Monitoring.Handlers;
using CK.MQTT;
using CK.MQTT.Client;
using CK.MQTT.Client.Tests.Helpers;
using System;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
#nullable enable

namespace SimpleClientTest
{
    class Program
    {

        static async Task Main()
        {
            //test.mosquitto.org

            ActivityMonitor m = new();
            var client = new TestMqttClient( new Mqtt5ClientConfiguration( "localhost:1883" ), Channel.CreateUnbounded<object?>().Writer )
            {
            };
            //client.DisconnectedHandler += ( reason, task ) =>
            //{
            //    System.Console.WriteLine( reason );
            //};
            var res = await client.ConnectAsync();
        }
    }
}
