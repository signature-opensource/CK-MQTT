using CK.Core;
using CK.MQTT.Client;
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
        /// Factory uses to create a MQTT Client.
        /// </summary>
        public static MqttClientFactory Factory { get; } = new MqttClientFactory();
    }

    public static class MqttClientFactories
    {
        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory? factory, MqttConfiguration config, Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => new MqttClientImpl( ProtocolConfiguration.Mqtt3, config, messageHandler );
        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory? factory, MqttConfiguration config, Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => new MqttClientImpl( ProtocolConfiguration.Mqtt5, config, messageHandler );
        public static IMqttClient CreateMQTTClient( this MqttClientFactory? factory, MqttConfiguration config, Func<IActivityMonitor, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => new MqttClientImpl( ProtocolConfiguration.Mqtt5, config, messageHandler );

        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory? factory, MqttConfiguration config, Func<IActivityMonitor, DisposableApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => factory.CreateMQTT3Client( config, new DisposableHandlerCancellableClosure( messageHandler ).HandleMessage );

        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory? factory, MqttConfiguration config, Func<IActivityMonitor, DisposableApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => factory.CreateMQTT5Client( config, new DisposableHandlerCancellableClosure( messageHandler ).HandleMessage );

        public static IMqttClient CreateMQTTClient( this MqttClientFactory? factory, MqttConfiguration config, Func<IActivityMonitor, DisposableApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => factory.CreateMQTTClient( config, new DisposableHandlerCancellableClosure( messageHandler ).HandleMessage );

        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory? factory, MqttConfiguration config, Func<IActivityMonitor, DisposableApplicationMessage, ValueTask> messageHandler )
            => factory.CreateMQTT3Client( config, new HandlerClosure( messageHandler ).HandleMessage );

        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory? factory, MqttConfiguration config, Func<IActivityMonitor, DisposableApplicationMessage, ValueTask> messageHandler )
            => factory.CreateMQTT5Client( config, new HandlerClosure( messageHandler ).HandleMessage );

        public static IMqttClient CreateMQTTClient( this MqttClientFactory? factory, MqttConfiguration config, Func<IActivityMonitor, DisposableApplicationMessage, ValueTask> messageHandler )
            => factory.CreateMQTTClient( config, new HandlerClosure( messageHandler ).HandleMessage );
    }

}
