using CK.Core;
using CK.Monitoring;
using CK.Monitoring.Handlers;
using CK.MQTT;
using System.Threading.Tasks;

namespace SimpleClientTest
{
    class Program
    {
        static async Task Main( string[] args )
        {
            var config = new GrandOutputConfiguration();
            config.Handlers.Add( new ConsoleConfiguration() );
            ActivityMonitor.DefaultFilter = LogFilter.Debug;
            config.MinimalFilter = LogFilter.Debug;
            var go = GrandOutput.EnsureActiveDefault( config );
            go.ExternalLogLevelFilter = LogLevelFilter.Debug;
            var m = new MqttActivityMonitor( new ActivityMonitor() );
            var client = new MqttClient( new MqttConfiguration( "test.mosquitto.org:1883" ) );
            var result = await await client.ConnectAsync( m, new MqttClientCredentials( "CKMqttTest", true ) );
            if( result.ConnectionStatus != ConnectReturnCode.Accepted )
            {
                return;
            }
            var returnSub = await await client.SubscribeAsync( m, new Subscription( "/test4712/#", QualityOfService.AtMostOnce ) );
            await await client.PublishAsync( m, new SimpleOutgoingApplicationMessage( false, true, "/test4712/42", QualityOfService.ExactlyOnce, () => 0, ( p, c ) => new ValueTask(), false ) );
            await client.DisconnectAsync( m );
            await Task.Delay( 3000 );
            result = await await client.ConnectAsync( m, new MqttClientCredentials( "CKMqttTest", false ) );
            await Task.Delay( 500 );
        }
    }
}
