using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.Client.Closures;
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

        public static P2PClient CreateMQTT3Client( this P2PMqttClientFactory factory, MqttClientConfiguration config, Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> handler )
            => factory.CreateMQTT3Client( config, new BaseHandlerClosure( handler ).HandleMessageAsync );

        public static P2PClient CreateMQTT5Client( this P2PMqttClientFactory @this, MqttClientConfiguration config, Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> handler )
            => @this.CreateMQTT5Client( config, new BaseHandlerClosure( handler ).HandleMessageAsync );

        public static P2PClient CreateMQTTClient( this P2PMqttClientFactory @this, MqttClientConfiguration config, Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> handler )
            => @this.CreateMQTTClient( config, new BaseHandlerClosure( handler ).HandleMessageAsync );

        public static P2PClient CreateMQTT3Client( this P2PMqttClientFactory? factory, MqttClientConfiguration config, Func<IActivityMonitor?, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => new( ProtocolConfiguration.Mqtt3, config, messageHandler );
        public static P2PClient CreateMQTT5Client( this P2PMqttClientFactory? factory, MqttClientConfiguration config, Func<IActivityMonitor?, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => new( ProtocolConfiguration.Mqtt5, config, messageHandler );
        public static P2PClient CreateMQTTClient( this P2PMqttClientFactory? factory, MqttClientConfiguration config, Func<IActivityMonitor?, string, PipeReader, int, QualityOfService, bool, CancellationToken, ValueTask> messageHandler )
            => new( ProtocolConfiguration.Mqtt5, config, messageHandler );

        public static P2PClient CreateMQTT3Client( this P2PMqttClientFactory? factory, MqttClientConfiguration config, Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => factory.CreateMQTT3Client( config, new DisposableMessageClosure( messageHandler ).HandleMessageAsync );

        public static P2PClient CreateMQTT5Client( this P2PMqttClientFactory? factory, MqttClientConfiguration config, Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => factory.CreateMQTT5Client( config, new DisposableMessageClosure( messageHandler ).HandleMessageAsync );

        public static P2PClient CreateMQTTClient( this P2PMqttClientFactory? factory, MqttClientConfiguration config, Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => factory.CreateMQTTClient( config, new DisposableMessageClosure( messageHandler ).HandleMessageAsync );

        public static P2PClient CreateMQTT3Client( this P2PMqttClientFactory @this, MqttClientConfiguration config, Func<IActivityMonitor?, ApplicationMessage, ValueTask> handler )
    => @this.CreateMQTT3Client( config, new MessagesClosure( handler ).HandleMessageAsync );

        public static P2PClient CreateMQTT5Client( this P2PMqttClientFactory @this, MqttClientConfiguration config, Func<IActivityMonitor?, ApplicationMessage, ValueTask> handler )
            => @this.CreateMQTT5Client( config, new MessagesClosure( handler ).HandleMessageAsync );

        public static P2PClient CreateMQTTClient( this P2PMqttClientFactory @this, MqttClientConfiguration config, Func<IActivityMonitor?, ApplicationMessage, ValueTask> handler )
            => @this.CreateMQTTClient( config, new MessagesClosure( handler ).HandleMessageAsync );
    }

}
