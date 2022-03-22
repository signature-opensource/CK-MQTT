using CK.Core;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests.Helpers
{
    [ExcludeFromCodeCoverage]
    static class MqttTestHelper
    {
        public static (PacketReplayer packetReplayer, SimpleTestMqtt3Client client) CreateTestClient( string channelType, Queue<PacketReplayer.TestWorker> packets )
        {
            PacketReplayer pcktReplayer = new( channelType, packets );
            var client = TestMqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ), ( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken cancellationToken ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            return (pcktReplayer, client);
        }

        public static async ValueTask<(PacketReplayer packetReplayer, SimpleTestMqtt3Client client)> CreateConnectedTestClient( string channelType, IEnumerable<PacketReplayer.TestWorker> packets )
        {
            PacketReplayer pcktReplayer = new
            (
                channelType,
                new Queue<PacketReplayer.TestWorker>( new[] {
                    TestPacketHelper.Outgoing( "20020000" ),
                    TestPacketHelper.Outgoing( "101600044d51545404020000000a434b4d71747454657374" )
                }.Concat( packets ) )
            );
            SimpleTestMqtt3Client client = TestMqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ), ( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken cancellationToken ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            await client.ConnectAsync(  );
            return (pcktReplayer, client);
        }
    }
}
