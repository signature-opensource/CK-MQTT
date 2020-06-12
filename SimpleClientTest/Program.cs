using CK.Core;
using CK.Monitoring;
using CK.Monitoring.Handlers;
using CK.MQTT;
using CK.MQTT.Client.Sdk;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Stores;
using System.Threading.Tasks;

namespace SimpleClientTest
{
    class Program
    {
        static async Task Main( string[] args )
        {
            var config = new GrandOutputConfiguration();
            config.Handlers.Add( new ConsoleConfiguration() );
            GrandOutput.EnsureActiveDefault( config );
            var m = new ActivityMonitor();
            var mqtt = new MqttClientOld( new TcpChannelFactory(), new VolatilePacketStoreManager(),
                new MqttConfiguration( "broker.mqttdashboard.com:8000" ) );
            await mqtt.ConnectAsync( m, new MqttClientCredentials( "testCkMqtt" ), cleanSession: true );
            await Task.Delay( 50000 );
        }
    }
}
