using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class MqttClientFactory
    {
        internal MqttClientFactory() { }

#pragma warning disable CA1822 // We want method to not be statics so we can offer extension method on it.

        public IMqtt3Client CreateMQTT3Client( MqttConfiguration config, Func<string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => new MqttClient( ProtocolConfiguration.Mqtt3, config, messageHandler );
        public IMqtt5Client CreateMQTT5Client( MqttConfiguration config, Func<string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => new MqttClient( ProtocolConfiguration.Mqtt5, config, messageHandler );
        public IMqttClient CreateMQTTClient( MqttConfiguration config, Func<string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => new MqttClient( ProtocolConfiguration.Mqtt5, config, messageHandler );

#pragma warning restore CA1822
    }
}
