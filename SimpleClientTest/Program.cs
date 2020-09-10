using CK.Core;
using CK.Monitoring;
using CK.Monitoring.Handlers;
using CK.MQTT;
using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

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
            var client = MqttClient.CreateMQTT3Client( new MqttConfiguration( "localhost:1883", TimeSpan.FromSeconds( 1 ), TimeSpan.FromMilliseconds( 45 ) )
            {
                InputLogger = new InputLoggerMqttActivityMonitor( new ActivityMonitor( "input" ) ),
                OutputLogger = new OutputLoggerMqttActivityMonitor( new ActivityMonitor( "output" ) )
            }, MessageHandlerDelegate );
            var result = await client.ConnectAsync( m, new MqttClientCredentials( "CKMqttTest", true ) );
            if( result.ConnectReturnCode != ConnectReturnCode.Accepted )
            {
                throw new Exception();
            }
            var returnSub = await await client.SubscribeAsync( m, new Subscription( "/test4712/#", QualityOfService.AtMostOnce ), new Subscription( "/test122/#", QualityOfService.ExactlyOnce ) );
            await await client.PublishAsync( m, "/test4712/42", QualityOfService.ExactlyOnce, false, () => 0, ( p, c ) => new ValueTask<IOutgoingPacket.WriteResult>( IOutgoingPacket.WriteResult.Written ) );
            await await client.UnsubscribeAsync( m, "test" );
            await Task.Delay( 300000 );
            await client.DisconnectAsync();
            await Task.Delay( 3000 );
            result = await client.ConnectAsync( m, new MqttClientCredentials( "CKMqttTest", false ) );
            await Task.Delay( 500 );
        }

        static ValueTask MessageHandlerDelegate( string topic, PipeReader pipeReader, int payloadLength, QualityOfService qos, bool retain )
        {
            System.Console.WriteLine( topic );
            return pipeReader.BurnBytes( payloadLength );
        }
    }
}
