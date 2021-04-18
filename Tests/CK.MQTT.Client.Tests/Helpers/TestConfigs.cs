using CK.Core;
namespace CK.MQTT.Client.Tests.Helpers
{
    static class TestConfigs
    {
        public static MqttClientConfiguration DefaultTestConfig( PacketReplayer packetReplayer, int timeoutMs = 5_000 )
        {
            ActivityMonitor iM = new( "Input Logger" );
            ActivityMonitor oM = new( "Output Logger" );

            _ = Testing.MonitorTestHelper.TestHelper.Monitor; // So the static will do GrandOutput.EnsureDefault.
            return new MqttClientConfiguration( "" )
            {
                ChannelFactory = packetReplayer,
                DelayHandler = packetReplayer.TestDelayHandler,
                StopwatchFactory = packetReplayer.TestDelayHandler,
                WaitTimeoutMilliseconds = timeoutMs,
                InputLogger = new InputLoggerMqttActivityMonitor( iM ),
                OutputLogger = new OutputLoggerMqttActivityMonitor( oM )
            };
        }
    }
}
