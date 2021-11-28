using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests.Helpers
{
    [ExcludeFromCodeCoverage]
    static class Scenario
    {

        static PacketReplayer CreateConnectedReplayer( string channelType, IEnumerable<PacketReplayer.TestWorker> packets ) => new( channelType, new[]
            {
                TestPacketHelper.Outgoing("101600044d5154540402001e000a434b4d71747454657374"),
                TestPacketHelper.SendToClient("20020000")
            }.Concat( packets ) );
        public static async Task<(PacketReplayer packetReplayer, IMqtt3Client client)> ConnectedClient( string channelType, IEnumerable<PacketReplayer.TestWorker> packets )
        {
            PacketReplayer pcktReplayer = CreateConnectedReplayer( channelType, packets );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ),
                NoOpDispose() );
            await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            return (pcktReplayer, client);
        }

        public static async Task<(PacketReplayer packetReplayer, IMqtt3Client client)> ConnectedClient( string channelType, IEnumerable<PacketReplayer.TestWorker> packets,
            Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> messageProcessor, Disconnected? disconnectedHandler = null )
        {
            PacketReplayer pcktReplayer = CreateConnectedReplayer( channelType, packets );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ),
                messageProcessor ?? NoOpDispose() );
            client.DisconnectedHandler += disconnectedHandler;
            await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            return (pcktReplayer, client);
        }

        public static async Task<(PacketReplayer packetReplayer, IMqtt3Client client)> ConnectedClient( string channelType, IEnumerable<PacketReplayer.TestWorker> packets,
            Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> messageProcessor, Disconnected? disconnectedHandler = null )
        {
            PacketReplayer pcktReplayer = CreateConnectedReplayer( channelType, packets );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ),
                messageProcessor ?? NoOpNew() );
            client.DisconnectedHandler += disconnectedHandler;
            await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            return (pcktReplayer, client);
        }

        static Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> NoOpNew()
            => ( IActivityMonitor? m, ApplicationMessage msg, CancellationToken cancellationToken ) =>
            {
                return new ValueTask();
            };

        static Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> NoOpDispose()
            => ( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken cancellationToken ) =>
            {
                msg.Dispose();
                return new ValueTask();
            };
    }
}
