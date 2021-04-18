using CK.Core;
using CK.MQTT;
using System;
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
            //var client = MqttClient.Factory.CreateMQTT3Client( new MqttConfiguration( "test.mosquitto.org:1883" ), MessageHandlerDelegate );
            //await client.ConnectAsync(null );
            //await await client.SubscribeAsync( null, new Subscription( "#", QualityOfService.AtMostOnce ) );
            //async static ValueTask MessageHandlerDelegate( IActivityMonitor? m, NewApplicationMessage msg, CancellationToken cancellationToken )
            //{
            //    Console.WriteLine( msg.Topic + Encoding.UTF8.GetString( msg.Payload.Span ) );
            //}
            //await Task.Delay( 200 );
            //await client.DisconnectAsync( null, true, true );
        }

        //    var config = new GrandOutputConfiguration();
        //    config.Handlers.Add(
        //        //new BinaryFileConfiguration
        //        //{
        //        //    Path = "Logs",
        //        //    MaxCountPerFile = 200_000_000,
        //        //    MaximumTotalKbToKeep = 1_000_000_000,
        //        //    UseGzipCompression = false
        //        //}
        //        //new ConsoleConfiguration()
        //        //{
        //        //    EnableMonitorIdColorFlag = true
        //        //}
        //        new TextFileConfiguration()
        //        {
        //            Path = "Logs",
        //            MaxCountPerFile = 20000000,
        //            MaximumTotalKbToKeep = 100000000
        //        }
        //        );

        //    ActivityMonitor.DefaultFilter = LogFilter.Debug;
        //    config.MinimalFilter = LogFilter.Debug;
        //    var go = GrandOutput.EnsureActiveDefault( config );
        //    go.ExternalLogLevelFilter = LogLevelFilter.Debug;
        //    ActivityMonitor? m = null; // new ActivityMonitor( "main" );
        //    var client = MqttClient.Factory.CreateMQTT3Client( new MqttClientConfiguration( "test.mosquitto.org:1883" )
        //    {
        //        InputLogger = null // new InputLoggerMqttActivityMonitor( new ActivityMonitor() )
        //        ,
        //        OutputLogger = null// new OutputLoggerMqttActivityMonitor( new ActivityMonitor() )
        //        ,
        //        KeepAliveSeconds = 0
        //    }, MessageHandlerDelegate );
        //    //Stopwatch stopwatch = new Stopwatch();
        //    var result = await client.ConnectAsync( m, new MqttClientCredentials( "CKMqttTest", true ) );
        //    //await await client.SubscribeAsync( m, new Subscription[] { new Subscription( "#", QualityOfService.AtMostOnce ) } );
        //    //await await client.PublishAsync( m, "test_topic", QualityOfService.ExactlyOnce, false, Encoding.ASCII.GetBytes( "hello world" ) );
        //    Stopwatch stopwatch = new();
        //    stopwatch.Start();
        //    var payload = Encoding.UTF8.GetBytes( "test payload" );
        //    //Task[] tasks = new Task[20000];
        //    for( int i = 0; i < 200_000; i++ )
        //    {
        //        await await client.PublishAsync( m, "test topic" + i, QualityOfService.ExactlyOnce, false, payload );
        //    }
        //    stopwatch.Stop();
        //    System.Console.WriteLine( "Elapsed:" + stopwatch.ElapsedMilliseconds );
        //}

        //async static ValueTask MessageHandlerDelegate( IActivityMonitor m, string topic, PipeReader pipeReader, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken )
        //{
        //    await pipeReader.SkipBytes( payloadLength );
        //}
    }
}
