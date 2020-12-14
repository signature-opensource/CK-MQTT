using CK.Core;
using CK.Monitoring;
using CK.Monitoring.Handlers;
using CK.MQTT;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
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
            var client = MqttClient.Factory.CreateMQTT3Client( new MqttConfiguration( "test.mosquitto.org:1883" )
            {
                InputLogger = new InputLoggerMqttActivityMonitor( new ActivityMonitor( "input" ) ),
                OutputLogger = new OutputLoggerMqttActivityMonitor( new ActivityMonitor( "output" ) ),
                KeepAliveSeconds = 0
            }, MessageHandlerDelegate );
            var result = await client.ConnectAsync( m, new MqttClientCredentials( "CKMqttTest", true ) );
            var res = await await client.SubscribeAsync( m, new Subscription( "#", QualityOfService.AtMostOnce ) );
            await Task.Delay( 500000 );
            //if( result.ConnectReturnCode != ConnectReturnCode.Accepted )
            //{
            //    throw new Exception();
            //}
            //var returnSub = await await client.SubscribeAsync( m, new Subscription( "/test4712/#", QualityOfService.AtMostOnce ), new Subscription( "/test122/#", QualityOfService.ExactlyOnce ) );
            //await await client.PublishAsync( m, "/test4712/42", QualityOfService.ExactlyOnce, false, () => 0, ( p, c ) => new ValueTask<IOutgoingPacket.WriteResult>( IOutgoingPacket.WriteResult.Written ) );
            //await await client.UnsubscribeAsync( m, "test" );
            //await Task.Delay( 3000 );
            //await client.DisconnectAsync();
            //await Task.Delay( 3000 );
            //result = await client.ConnectAsync( m, new MqttClientCredentials( "CKMqttTest", false ) );
            //await Task.Delay( 500 );
        }

        static bool first = true;
        async static ValueTask MessageHandlerDelegate( IActivityMonitor m, string topic, PipeReader pipeReader, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken )
        {
            if( first )
            {
                await Task.Delay( 5000 );
                first = false;
            }
            m.Info( "********" + topic + "********payload" + payloadLength );
            await pipeReader.SkipBytes( payloadLength );
        }
    }
}
