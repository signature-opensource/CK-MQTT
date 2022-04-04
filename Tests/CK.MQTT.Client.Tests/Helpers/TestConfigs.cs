namespace CK.MQTT.Client.Tests.Helpers
{
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
                StopwatchFactory = packetReplayer.TestTimeHandler,
                CancellationTokenSourceFactory = packetReplayer.TestTimeHandler,
                WaitTimeoutMilliseconds = timeoutMs,
                KeepAliveSeconds = 0,
                Credentials = credentials ?? new MqttClientCredentials( "CKMqttTest", true )
            };
        }

        public static Mqtt5ClientConfiguration MQTT5Config(
            PacketReplayer packetReplayer,
            int timeoutMs = 5_000,
            MqttClientCredentials? credentials = null )
        {
            _ = Testing.MonitorTestHelper.TestHelper; // So the static will do GrandOutput.EnsureDefault.
            return new Mqtt5ClientConfiguration( "" )
            {
                ChannelFactory = packetReplayer,
                StopwatchFactory = packetReplayer.TestTimeHandler,
                CancellationTokenSourceFactory = packetReplayer.TestTimeHandler,
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
                StopwatchFactory = packetReplayer.TestTimeHandler,
                CancellationTokenSourceFactory = packetReplayer.TestTimeHandler,
                WaitTimeoutMilliseconds = timeoutMs,
                KeepAliveSeconds = 5,
                Credentials = credentials ?? new MqttClientCredentials( "CKMqttTest", true ),
                DisconnectBehavior = disconnectBehavior
            };
        }
    }
}
