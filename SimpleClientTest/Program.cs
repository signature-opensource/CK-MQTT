using CK.Core;
using CK.Monitoring;
using CK.Monitoring.Handlers;
using CK.MQTT;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#nullable enable

namespace SimpleClientTest
{
    class Program
    {
        static async Task Main()
        {
            var config = new GrandOutputConfiguration();
            config.Handlers.Add( new ConsoleConfiguration()
            {
                EnableMonitorIdColorFlag = true
            } );
            ActivityMonitor.DefaultFilter = LogFilter.Debug;
            config.MinimalFilter = LogFilter.Debug;
            var go = GrandOutput.EnsureActiveDefault( config );
            go.ExternalLogLevelFilter = LogLevelFilter.Debug;
            var m = new ActivityMonitor( "main" );
            var client = MqttClient.Factory.CreateMQTT3Client( new MqttConfiguration( "localhost:1883" )
            {
                InputLogger = null,
                OutputLogger = null,
                KeepAliveSeconds = 0
            }, MessageHandlerDelegate );
            Stopwatch stopwatch = new Stopwatch();
            var result = await client.ConnectAsync( null, new MqttClientCredentials( "CKMqttTest", true ) );
            var payload = Encoding.UTF8.GetBytes( "test payload" );
            for( int i = 0; i < 20000; i++ )
            {
                await await client.PublishAsync( null, "test topic", QualityOfService.ExactlyOnce, false, payload );
            }
        }

        async static ValueTask MessageHandlerDelegate( IActivityMonitor m, string topic, PipeReader pipeReader, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken )
        {
            await pipeReader.SkipBytes( payloadLength );
        }
    }
}
