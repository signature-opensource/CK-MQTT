namespace CK.MQTT.Client.Tests.Helpers
{
    static class TestConfigs
    {
        public static MQTT3ClientConfiguration DefaultTestConfig(
            PacketReplayer packetReplayer,
            int timeoutMs = 5_000 )
        {
            _ = Testing.MonitorTestHelper.TestHelper; // So the static will do GrandOutput.EnsureDefault.
            return new MQTT3ClientConfiguration()
            {
                TimeUtilities = packetReplayer.TestTimeHandler,
                WaitTimeoutMilliseconds = timeoutMs,
                KeepAliveSeconds = 0,
                ClientId = "CKMqttTest"
            };
        }

        public static MQTT5ClientConfiguration MQTT5Config(
            PacketReplayer packetReplayer,
            int timeoutMs = 5_000 )
        {
            _ = Testing.MonitorTestHelper.TestHelper; // So the static will do GrandOutput.EnsureDefault.
            return new MQTT5ClientConfiguration()
            {
                TimeUtilities = packetReplayer.TestTimeHandler,
                WaitTimeoutMilliseconds = timeoutMs,
                KeepAliveSeconds = 0,
                ClientId = "CKMqttTest"
            };
        }

        public static MQTT3ClientConfiguration DefaultTestConfigWithKeepAlive( PacketReplayer packetReplayer,
                                                                              int timeoutMs = 5_000,
                                                                              DisconnectBehavior disconnectBehavior = DisconnectBehavior.Nothing )
        {
            _ = Testing.MonitorTestHelper.TestHelper; // So the static will do GrandOutput.EnsureDefault.
            return new MQTT3ClientConfiguration()
            {
                TimeUtilities = packetReplayer.TestTimeHandler,
                WaitTimeoutMilliseconds = timeoutMs,
                KeepAliveSeconds = 5,
                ClientId = "CKMqttTest"
            };
        }
    }
}
