using CK.Core;
namespace CK.MQTT.Client.Tests.Helpers
{
    static class TestConfigs
    {
        public static MqttConfiguration DefaultTestConfig( PacketReplayer packetReplayer, int timeoutMs = 5_000 )
        {
            _ = Testing.MonitorTestHelper.TestHelper.Monitor; // So the static will do GrandOutput.EnsureDefault.
            return new MqttConfiguration( "" )
            {
                ChannelFactory = packetReplayer,
                DelayHandler = packetReplayer.TestDelayHandler,
                StopwatchFactory = packetReplayer.TestDelayHandler,
                WaitTimeoutMilliseconds = timeoutMs,
                InputLogger = new InputLoggerMqttActivityMonitor( new ActivityMonitor( "Input Logger" ) ),
                OutputLogger = new OutputLoggerMqttActivityMonitor( new ActivityMonitor( "Output Logger" ) )
            };
        }
    }
}
