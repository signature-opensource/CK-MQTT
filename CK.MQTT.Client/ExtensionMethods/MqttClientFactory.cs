using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.Client.Closures;
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

    public static class MqttClient
    {
        /// <summary>
        /// Factory to create a MQTT Client.
        /// </summary>
        public static MqttClientFactory Factory { get; } = new MqttClientFactory();
    }

    public static class MqttClientFactories
    {
        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory factory, MqttClientConfiguration config, Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> handler )
            => factory.CreateMQTT3Client( config, new BaseHandlerClosure( handler ).HandleMessageAsync );

        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory @this, MqttClientConfiguration config, Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> handler )
            => @this.CreateMQTT5Client( config, new BaseHandlerClosure( handler ).HandleMessageAsync );

        public static IMqttClient CreateMQTTClient( this MqttClientFactory @this, MqttClientConfiguration config, Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> handler )
            => @this.CreateMQTTClient( config, new BaseHandlerClosure( handler ).HandleMessageAsync );

        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory? factory, MqttClientConfiguration config, Func<IActivityMonitor?, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => new MqttClientImpl( ProtocolConfiguration.Mqtt3, config, messageHandler );
        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory? factory, MqttClientConfiguration config, Func<IActivityMonitor?, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => new MqttClientImpl( ProtocolConfiguration.Mqtt5, config, messageHandler );
        public static IMqttClient CreateMQTTClient( this MqttClientFactory? factory, MqttClientConfiguration config, Func<IActivityMonitor?, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => new MqttClientImpl( ProtocolConfiguration.Mqtt5, config, messageHandler );

        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory? factory, MqttClientConfiguration config, Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => factory.CreateMQTT3Client( config, new DisposableMessageClosure( messageHandler ).HandleMessageAsync );

        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory? factory, MqttClientConfiguration config, Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => factory.CreateMQTT5Client( config, new DisposableMessageClosure( messageHandler ).HandleMessageAsync );

        public static IMqttClient CreateMQTTClient( this MqttClientFactory? factory, MqttClientConfiguration config, Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => factory.CreateMQTTClient( config, new DisposableMessageClosure( messageHandler ).HandleMessageAsync );
    }
}
