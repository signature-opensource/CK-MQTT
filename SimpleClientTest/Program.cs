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
using System.Threading.Tasks;
#nullable enable

namespace SimpleClientTest
{
    class Program
    {
        bool _isLowLevel;
        async ValueTask MessageHandlerDelegate( string topic, PipeReader pipe, uint payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
        {
            if( !topic.Contains( "lowlevel" ) )
            {
                await new DisposableMessageClosure( OtherHandler ).HandleMessageAsync( topic, pipe, payloadLength, qos, retain, cancelToken );
            }
            else
            {
                // low level handling
            }
        }

        static ValueTask OtherHandler( IActivityMonitor? m, DisposableApplicationMessage message, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
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
            //client.DisconnectedHandler += ( reason, task ) =>
            //{
            //    System.Console.WriteLine( reason );
            //};
            var res = await client.ConnectAsync( m );
            await Task.Delay( 3000000 );
            await client.DisconnectAsync( m, true, true, default );
        }
    }
}
