using CK.Core;
using CK.Monitoring;
using CK.Monitoring.Handlers;
using CK.MQTT;
using CK.MQTT.Client;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Stores;
using System.IO.Pipelines;
using System.Net.Sockets;
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
            var client = MqttClient.Create(
                new InMemoryPacketIdStore(),
                new MemoryPacketStore( ushort.MaxValue ),
                new MqttConfiguration( "broker.hivemq.com:1883" ),
                new TcpChannelFactory() );
            var result = await client.ConnectAsync( m, new MqttClientCredentials( "CKMqttTest", true ) );
            if( result.ConnectionStatus != ConnectReturnCode.Accepted )
            {

            }
            var returnSub = await await client.SubscribeAsync( m, new Subscription( "#", QualityOfService.ExactlyOnce ) );

            await Task.Delay( 50000 );
        }
    }
}
