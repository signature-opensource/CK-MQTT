using CK.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests.Helpers
{
    static class MqttTestHelper
    {
        public static (PacketReplayer packetReplayer, IMqtt3Client client) CreateTestClient( Queue<TestPacket> packets )
        {
            PacketReplayer pcktReplayer = new( packets );
            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ), (IActivityMonitor m, DisposableApplicationMessage msg ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            return (pcktReplayer, client);
        }
        public static async ValueTask<(PacketReplayer packetReplayer, IMqtt3Client client)> CreateConnectedTestClient( IEnumerable<TestPacket> packets )
        {
            PacketReplayer pcktReplayer = new
            (
                new Queue<TestPacket>( new[] {
                    TestPacket.Outgoing( "20020000" ),
                    TestPacket.Outgoing( "101600044d5154540402001e000a434b4d71747454657374" )
                }.Concat( packets ) )
            );
            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ), (IActivityMonitor m, DisposableApplicationMessage msg ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            return (pcktReplayer, client);
        }
    }
}
