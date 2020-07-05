using CK.Core;
using CK.Monitoring;
using CK.Monitoring.Handlers;
using CK.MQTT.Client;
using System.Threading.Tasks;

namespace SimpleClientTest
{
    class Program
    {
        static async Task Main( string[] args )
        {
            var config = new GrandOutputConfiguration();
            config.Handlers.Add( new ConsoleConfiguration() );
            ActivityMonitor.DefaultFilter = LogFilter.Trace;
            config.MinimalFilter = LogFilter.Trace;
            var go = GrandOutput.EnsureActiveDefault( config );
            go.ExternalLogLevelFilter = LogLevelFilter.Trace;
            var m = new ActivityMonitor();
            m.Info( "letsgo" );
            var client = new MqttClient(
                new InMemoryPacketIdStore(),
                new MemoryPacketStore( ushort.MaxValue ),
                //new MqttConfiguration( "broker.hivemq.com:1883" ),
                new MqttConfiguration( "test.mosquitto.org:1883" ),
                new TcpChannelFactory() );
            var result = await await client.ConnectAsync( m, new MqttClientCredentials( "CKMqttTest", true ) );
            if( result.ConnectionStatus != ConnectReturnCode.Accepted )
            {
                return;
            }
            var returnSub = await await client.SubscribeAsync( m, new Subscription( "/test4712/#", QualityOfService.AtMostOnce ) );
            await await client.PublishAsync( m, new SimpleOutgoingApplicationMessage( false, true, "/test4712/42", QualityOfService.ExactlyOnce, () => 0, ( p, c ) => new ValueTask(), false ) );
            await client.DisconnectAsync( m );
            result = await await client.ConnectAsync( m, new MqttClientCredentials( "CKMqttTest", false ) );
        }
    }
}
