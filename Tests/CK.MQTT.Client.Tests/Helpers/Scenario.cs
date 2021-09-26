using CK.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests.Helpers
{
    static class Scenario
    {
        public static async Task<(PacketReplayer packetReplayer, IMqtt3Client client)> ConnectedClient( IEnumerable<TestPacket> packets )
        {
            PacketReplayer pcktReplayer = new( new Queue<TestPacket>( new List<TestPacket>()
            {
                TestPacket.Outgoing("101600044d5154540402001e000a434b4d71747454657374"),
                TestPacket.Incoming("20020000")
            }.Concat( packets ) ) );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ), ( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken cancellationToken ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            return (pcktReplayer, client);
        }
    }
}
