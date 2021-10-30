using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests.Helpers
{
    static class Scenario
    {
        public static async Task<(PacketReplayer packetReplayer, IMqtt3Client client)> ConnectedClient(string channelType, IEnumerable<PacketReplayer.TestWorker> packets,
            Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask>?  messageProcessor = null )
        {
            PacketReplayer pcktReplayer = new(channelType, new[]
            {
                TestPacketHelper.Outgoing("101600044d5154540402001e000a434b4d71747454657374"),
                TestPacketHelper.SendToClient("20020000")
            }.Concat( packets ) );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ),
                messageProcessor ?? NoOp() );
            await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            return (pcktReplayer, client);
        }

        private static Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> NoOp()
            => ( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken cancellationToken ) =>
            {
                msg.Dispose();
                return new ValueTask();
            };
    }
}
