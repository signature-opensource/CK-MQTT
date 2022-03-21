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
            IInputLogger? inputLogger = null,
            IOutputLogger? outputLogger = null,
            MqttClientCredentials? credentials = null )
        {
            _ = Testing.MonitorTestHelper.TestHelper.Monitor; // So the static will do GrandOutput.EnsureDefault.
            return new MqttClientConfiguration( "" )
            {
                ChannelFactory = packetReplayer,
                DelayHandler = packetReplayer.TestDelayHandler,
                StopwatchFactory = packetReplayer.TestDelayHandler,
                CancellationTokenSourceFactory = packetReplayer.TestDelayHandler,
                WaitTimeoutMilliseconds = timeoutMs,
                KeepAliveSeconds = 0,
                InputLogger = inputLogger ?? new InputLoggerMqttActivityMonitor( new ActivityMonitor( "Input Logger" ) ),
                OutputLogger = outputLogger ?? new OutputLoggerMqttActivityMonitor( new ActivityMonitor( "Output Logger" ) ),
                Credentials = credentials ?? new MqttClientCredentials( "CKMqttTest", true )
            };
        }

        public static Mqtt3ClientConfiguration DefaultTestConfigWithKeepAlive( PacketReplayer packetReplayer,
                                                                              int timeoutMs = 5_000,
                                                                              IInputLogger? inputLogger = null,
                                                                              IOutputLogger? outputLogger = null,
                                                                              MqttClientCredentials? credentials = null,
                                                                              DisconnectBehavior disconnectBehavior = DisconnectBehavior.Nothing )
        {
            _ = Testing.MonitorTestHelper.TestHelper.Monitor; // So the static will do GrandOutput.EnsureDefault.
            return new MqttClientConfiguration( "" )
            {
                ChannelFactory = packetReplayer,
                DelayHandler = packetReplayer.TestDelayHandler,
                StopwatchFactory = packetReplayer.TestDelayHandler,
                CancellationTokenSourceFactory = packetReplayer.TestDelayHandler,
                WaitTimeoutMilliseconds = timeoutMs,
                KeepAliveSeconds = 5,
                InputLogger = inputLogger ?? new InputLoggerMqttActivityMonitor( new ActivityMonitor( "Input Logger" ) ),
                OutputLogger = outputLogger ?? new OutputLoggerMqttActivityMonitor( new ActivityMonitor( "Output Logger" ) ),
                Credentials = credentials ?? new MqttClientCredentials( "CKMqttTest", true ),
                DisconnectBehavior = disconnectBehavior
            };
        }
    }
}
