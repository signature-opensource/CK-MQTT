namespace CK.MQTT.Client.Tests.Helpers
{
    static class TestConfigs
    {
        public static MQTT3ClientConfiguration DefaultTestConfig(
            PacketReplayer packetReplayer,
            int timeoutMs = 5_000,
            MQTTClientCredentials? credentials = null )
        {
            _ = Testing.MonitorTestHelper.TestHelper; // So the static will do GrandOutput.EnsureDefault.
            return new MQTT3ClientConfiguration()
            {
                TimeUtilities = packetReplayer.TestTimeHandler,
                WaitTimeoutMilliseconds = timeoutMs,
                KeepAliveSeconds = 0,
                Credentials = credentials ?? new MQTTClientCredentials( "CKMQTTTest", true ),
                ManualConnectBehavior = ManualConnectBehavior.TryOnce
            };
        }

        public static MQTT5ClientConfiguration MQTT5Config(
            PacketReplayer packetReplayer,
            int timeoutMs = 5_000,
            MQTTClientCredentials? credentials = null )
        {
            _ = Testing.MonitorTestHelper.TestHelper; // So the static will do GrandOutput.EnsureDefault.
            return new MQTT5ClientConfiguration()
            {
                TimeUtilities = packetReplayer.TestTimeHandler,
                WaitTimeoutMilliseconds = timeoutMs,
                KeepAliveSeconds = 0,
                Credentials = credentials ?? new MQTTClientCredentials( "CKMQTTTest", true )
            };
        }

        public static MQTT3ClientConfiguration DefaultTestConfigWithKeepAlive( PacketReplayer packetReplayer,
                                                                              int timeoutMs = 5_000,
                                                                              MQTTClientCredentials? credentials = null,
                                                                              DisconnectBehavior disconnectBehavior = DisconnectBehavior.Nothing )
        {
            _ = Testing.MonitorTestHelper.TestHelper; // So the static will do GrandOutput.EnsureDefault.
            return new MQTT3ClientConfiguration()
            {
                TimeUtilities = packetReplayer.TestTimeHandler,
                WaitTimeoutMilliseconds = timeoutMs,
                KeepAliveSeconds = 5,
                Credentials = credentials ?? new MQTTClientCredentials( "CKMQTTTest", true ),
                DisconnectBehavior = disconnectBehavior
            };
        }
    }
}
