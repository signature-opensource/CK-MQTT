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
                TestPacketHelper.Outgoing("101600044d51545404020000000a434b4d71747454657374"),
                TestPacketHelper.SendToClient("20020000")
            }.Concat( packets ) );
        public static async Task<(PacketReplayer packetReplayer, IMqtt3Client client)> ConnectedClient( string channelType, IEnumerable<PacketReplayer.TestWorker> packets )
        {
            PacketReplayer pcktReplayer = CreateConnectedReplayer( channelType, packets );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ),
                ClientHelpers.NotListening_Dispose );
            await client.ConnectAsync( TestHelper.Monitor );
            return (pcktReplayer, client);
        }

        public static async Task<(PacketReplayer packetReplayer, IMqtt3Client client)> ConnectedClient( string channelType,
            IEnumerable<PacketReplayer.TestWorker> packets,
            Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> messageProcessor,
            Disconnected? disconnectedHandler = null )
        {
            PacketReplayer pcktReplayer = CreateConnectedReplayer( channelType, packets );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ),
                messageProcessor ?? ClientHelpers.NotListening_Dispose );
            client.DisconnectedHandler += disconnectedHandler;
            await client.ConnectAsync( TestHelper.Monitor );
            return (pcktReplayer, client);
        }

        public static async Task<(PacketReplayer packetReplayer, IMqtt3Client client)> ConnectedClient( string channelType, IEnumerable<PacketReplayer.TestWorker> packets,
            Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> messageProcessor, Disconnected? disconnectedHandler = null )
        {
            PacketReplayer pcktReplayer = CreateConnectedReplayer( channelType, packets );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ),
                messageProcessor ?? ClientHelpers.NotListening_New );
            client.DisconnectedHandler += disconnectedHandler;
            await client.ConnectAsync( TestHelper.Monitor );
            return (pcktReplayer, client);
        }

        static Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> NoOpNew()
            => ( IActivityMonitor? m, ApplicationMessage msg, CancellationToken cancellationToken ) =>
            {
                return new ValueTask();
            };

        public static async Task RunOnConnectedClientWithKeepAlive( string channelType, IEnumerable<PacketReplayer.TestWorker> packets, DisconnectBehavior disconnectBehavior = DisconnectBehavior.Nothing )
        {
            PacketReplayer pcktReplayer = new( channelType, new[]
            {
                TestPacketHelper.Outgoing("101600044d51545404020005000a434b4d71747454657374"),
                TestPacketHelper.SendToClient("20020000")
            } );
            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfigWithKeepAlive( pcktReplayer, disconnectBehavior: disconnectBehavior ), ClientHelpers.NotListening_Dispose );
            pcktReplayer.Client = client;
            foreach( var item in packets )
            {
                await pcktReplayer.PacketsWorker.Writer.WriteAsync( item );
            }
            await client.ConnectAsync( TestHelper.Monitor );
            await pcktReplayer.StopAndEnsureValidAsync();
        }
    }
}
