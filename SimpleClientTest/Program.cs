using CK.Core;
using CK.Monitoring;
using CK.Monitoring.Handlers;
using CK.MQTT;
using System;
using System.Collections.Generic;
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
            ActivityMonitor? m = null; //new ActivityMonitor( "main" );
            var client = MqttClient.Factory.CreateMQTT3Client( new MqttClientConfiguration( "localhost:1883" )
            {
                InputLogger = new InputLoggerMqttActivityMonitor( new ActivityMonitor() ),
                OutputLogger = new OutputLoggerMqttActivityMonitor( new ActivityMonitor() ),
                KeepAliveSeconds = 0
            }, MessageHandlerDelegate );
            //Stopwatch stopwatch = new Stopwatch();
            var result = await client.ConnectAsync( m, new MqttClientCredentials( "CKMqttTest", true ) );
            //await await client.SubscribeAsync( m, new Subscription[] { new Subscription( "#", QualityOfService.AtMostOnce ) } );
            await await client.PublishAsync( m, "test_topic", QualityOfService.ExactlyOnce, false, Encoding.ASCII.GetBytes( "hello world" ) );
            var payload = Encoding.UTF8.GetBytes( "test payload" );
            for( int i = 0; i < 200000; i++ )
            {
                await await client.PublishAsync( null, "test topic", QualityOfService.ExactlyOnce, false, payload );
            }
            await Task.Delay( 5000000 );
        }

        static Random r = new();

        async static ValueTask MessageHandlerDelegate( IActivityMonitor m, string topic, PipeReader pipeReader, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken )
        {
            if( r.Next() % 15 == 0 )
            {
                System.Console.WriteLine( topic );
            }
            await pipeReader.SkipBytes( payloadLength );
        }
    }
}
