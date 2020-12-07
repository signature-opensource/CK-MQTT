namespace CK.MQTT.Client.Tests.Helpers
{
    static class TestConfigs
    {
        public static MqttConfiguration DefaultTestConfig( PacketReplayer packetReplayer ) => new MqttConfiguration( "" )
        {
            ChannelFactory = packetReplayer,
            DelayHandler = packetReplayer.TestDelayHandler,
            StopwatchFactory = packetReplayer.TestDelayHandler
        };
    }
}
