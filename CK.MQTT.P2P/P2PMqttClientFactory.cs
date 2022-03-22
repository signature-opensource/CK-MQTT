using CK.MQTT.Client;
using CK.MQTT.P2P;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class P2PMqttClientFactory
    {
        internal P2PMqttClientFactory() { }
    }

    public static class P2PMqttClient
    {
        /// <summary>
        /// Factory to create a MQTT Client.
        /// </summary>
        public static P2PMqttClientFactory Factory { get; } = new P2PMqttClientFactory();
    }

    public static class MqttClientFactories
    {

        public static P2PClient CreateMQTTClient( this P2PMqttClientFactory? factory, IMqtt5ServerClientSink sink, P2PMqttConfiguration config, Func<string, PipeReader, uint, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => new(sink, config, messageHandler );
    }

}
