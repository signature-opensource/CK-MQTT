using CK.Core;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class MqttClientFactory
    {
        internal MqttClientFactory() { }
    }

    public static class MqttClientFactories
    {
        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory factory, MqttConfiguration config, Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => new MqttClient( ProtocolConfiguration.Mqtt3, config, messageHandler );
        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory factory, MqttConfiguration config, Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => new MqttClient( ProtocolConfiguration.Mqtt5, config, messageHandler );
        public static IMqttClient CreateMQTTClient( this MqttClientFactory factory, MqttConfiguration config, Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => new MqttClient( ProtocolConfiguration.Mqtt5, config, messageHandler );
    }

}
