using CK.Core;
using System.Diagnostics.CodeAnalysis;

namespace CK.MQTT.Client.Tests.Helpers
{
    [ExcludeFromCodeCoverage]
    static class TestConfigs
    {
        public static Mqtt3ClientConfiguration DefaultTestConfig(
            PacketReplayer packetReplayer,
            int timeoutMs = 5_000,
            MqttClientCredentials? credentials = null )
        {
            _ = Testing.MonitorTestHelper.TestHelper; // So the static will do GrandOutput.EnsureDefault.
            return new Mqtt3ClientConfiguration( "" )
            {
                ChannelFactory = packetReplayer,
                DelayHandler = packetReplayer.TestDelayHandler,
                StopwatchFactory = packetReplayer.TestDelayHandler,
                CancellationTokenSourceFactory = packetReplayer.TestDelayHandler,
                WaitTimeoutMilliseconds = timeoutMs,
                KeepAliveSeconds = 0,
                Credentials = credentials ?? new MqttClientCredentials( "CKMqttTest", true )
            };
        }

        public static Mqtt3ClientConfiguration DefaultTestConfigWithKeepAlive( PacketReplayer packetReplayer,
                                                                              int timeoutMs = 5_000,
                                                                              MqttClientCredentials? credentials = null,
                                                                              DisconnectBehavior disconnectBehavior = DisconnectBehavior.Nothing )
        {
            _ = Testing.MonitorTestHelper.TestHelper; // So the static will do GrandOutput.EnsureDefault.
            return new Mqtt3ClientConfiguration( "" )
            {
                ChannelFactory = packetReplayer,
                DelayHandler = packetReplayer.TestDelayHandler,
                StopwatchFactory = packetReplayer.TestDelayHandler,
                CancellationTokenSourceFactory = packetReplayer.TestDelayHandler,
                WaitTimeoutMilliseconds = timeoutMs,
                KeepAliveSeconds = 5,
                Credentials = credentials ?? new MqttClientCredentials( "CKMqttTest", true ),
                DisconnectBehavior = disconnectBehavior
            };
        }
    }
}
