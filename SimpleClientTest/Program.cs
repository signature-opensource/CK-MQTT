using CK.Core;
using CK.Monitoring;
using CK.Monitoring.Handlers;
using CK.MQTT;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Http;
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

            //var config = new GrandOutputConfiguration();
            //config.Handlers.Add(
            //     new ConsoleConfiguration()
            //     {
            //         EnableMonitorIdColorFlag = true
            //     }
            //    //new TextFileConfiguration()
            //    //{
            //    //    Path = "Logs"
            //    //}
            //    );
            //ActivityMonitor.DefaultFilter = LogFilter.Debug;
            //config.MinimalFilter = LogFilter.Debug;
            //var go = GrandOutput.EnsureActiveDefault( config );
            //go.ExternalLogLevelFilter = LogLevelFilter.Debug;
            //ActivityMonitor? m = null;// new ActivityMonitor( "main" );
            var client = MqttClient.Factory.CreateMQTT3Client( new MqttClientConfiguration( "localhost:1883" )
            {
                InputLogger = null// new InputLoggerMqttActivityMonitor( new ActivityMonitor() )
                ,
                OutputLogger = null// new OutputLoggerMqttActivityMonitor( new ActivityMonitor() )
                ,
                KeepAliveSeconds = 0
            }, MessageHandlerDelegate );
            //Stopwatch stopwatch = new Stopwatch();
            var result = await client.ConnectAsync( null, new MqttClientCredentials( "CKMqttTest", true ) );
            //await await client.SubscribeAsync( m, new Subscription[] { new Subscription( "#", QualityOfService.AtMostOnce ) } );
            //await await client.PublishAsync( m, "test_topic", QualityOfService.ExactlyOnce, false, Encoding.ASCII.GetBytes( "hello world" ) );
            Stopwatch stopwatch = new();
            stopwatch.Start();
            var payload = Encoding.UTF8.GetBytes( "test payload" );
            //Task[] tasks = new Task[20000];

            await await client.SendPacketAsync<object>( null, LifecyclePacketV3.Pubrel( 3712 ) );
            await Task.Delay( 2000 );
            for( int i = 0; i < 2_000_000; i++ )
            {
                await await client.PublishAsync( null, "test topic" + i, QualityOfService.ExactlyOnce, false, payload );
            }
            stopwatch.Stop();
            System.Console.WriteLine( "Elapsed:" + stopwatch.ElapsedMilliseconds );
        }

        static readonly Random _r = new();

        async static ValueTask MessageHandlerDelegate( IActivityMonitor m, string topic, PipeReader pipeReader, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken )
        {
            if( _r.Next() % 15 == 0 )
            {
                System.Console.WriteLine( topic );
            }
            await pipeReader.SkipBytesAsync( payloadLength );
        }
    }
}
