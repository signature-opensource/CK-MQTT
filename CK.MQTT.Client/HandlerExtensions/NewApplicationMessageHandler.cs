using CK.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public record NewApplicationMessage( string Topic, ReadOnlyMemory<byte> Payload, QualityOfService QoS, bool Retain );

    public static class NewApplicationMessageExtensions
    {
        public static ValueTask<Task> PublishAsync( this IMqtt3Client @this, IActivityMonitor m, NewApplicationMessage message )
            => @this.PublishAsync( m, message.Topic, message.QoS, message.Retain, message.Payload );

        class HandlerCancellableClosure
        {
            readonly Func<IActivityMonitor, NewApplicationMessage, CancellationToken, ValueTask> _messageHandler;
            public HandlerCancellableClosure( Func<IActivityMonitor, NewApplicationMessage, CancellationToken, ValueTask> messageHandler )
                => _messageHandler = messageHandler;

            public ValueTask HandleMessage( IActivityMonitor m, string t, ReadOnlyMemory<byte> b, QualityOfService q, bool r, CancellationToken c )
                => _messageHandler( m, new NewApplicationMessage( t, b, q, r ), c );
        }

        public static void SetMessageHandler( this IMqtt3Client @this, Func<IActivityMonitor, NewApplicationMessage, CancellationToken, ValueTask> handler )
            => @this.SetMessageHandler( new HandlerCancellableClosure( handler ).HandleMessage );

        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory factory, MqttConfiguration config, Func<IActivityMonitor, NewApplicationMessage, CancellationToken, ValueTask> handler )
            => factory.CreateMQTT3Client( config, new HandlerCancellableClosure( handler ).HandleMessage );

        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory @this, MqttConfiguration config, Func<IActivityMonitor, NewApplicationMessage, CancellationToken, ValueTask> handler )
            => @this.CreateMQTT5Client( config, new HandlerCancellableClosure( handler ).HandleMessage );

        public static IMqttClient CreateMQTTClient( this MqttClientFactory @this, MqttConfiguration config, Func<IActivityMonitor, NewApplicationMessage, CancellationToken, ValueTask> handler )
            => @this.CreateMQTTClient( config, new HandlerCancellableClosure( handler ).HandleMessage );

        class HandlerClosure
        {
            readonly Func<IActivityMonitor, NewApplicationMessage, ValueTask> _messageHandler;
            public HandlerClosure( Func<IActivityMonitor, NewApplicationMessage, ValueTask> messageHandler )
                => _messageHandler = messageHandler;

            public ValueTask HandleMessage( IActivityMonitor m, string t, ReadOnlyMemory<byte> b, QualityOfService q, bool r, CancellationToken c )
                => _messageHandler( m, new NewApplicationMessage( t, b, q, r ) );
        }

        public static void SetMessageHandler( this IMqtt3Client @this, Func<IActivityMonitor, NewApplicationMessage, ValueTask> handler )
            => @this.SetMessageHandler( new HandlerClosure( handler ).HandleMessage );

        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory @this, MqttConfiguration config, Func<IActivityMonitor, NewApplicationMessage, ValueTask> handler )
            => @this.CreateMQTT3Client( config, new HandlerClosure( handler ).HandleMessage );

        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory @this, MqttConfiguration config, Func<IActivityMonitor, NewApplicationMessage, ValueTask> handler )
            => @this.CreateMQTT5Client( config, new HandlerClosure( handler ).HandleMessage );

        public static IMqttClient CreateMQTTClient( this MqttClientFactory @this, MqttConfiguration config, Func<IActivityMonitor, NewApplicationMessage, ValueTask> handler )
            => @this.CreateMQTTClient( config, new HandlerClosure( handler ).HandleMessage );
    }
}
