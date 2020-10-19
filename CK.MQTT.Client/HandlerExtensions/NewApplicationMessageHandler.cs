using CK.Core;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class NewApplicationMessage
    {
        public NewApplicationMessage( string topic, ReadOnlyMemory<byte> payload, QualityOfService qoS, bool retain )
        {
            Topic = topic;
            Payload = payload;
            QoS = qoS;
            Retain = retain;
        }
        public string Topic { get; }
        public ReadOnlyMemory<byte> Payload { get; }
        public QualityOfService QoS { get; }
        public bool Retain { get; }
    }

    public static class NewApplicationMessageExtensions
    {
        public static ValueTask<Task> PublishAsync( this IMqtt3Client client, IActivityMonitor m, NewApplicationMessage message )
            => client.PublishAsync( m, message.Topic, message.QoS, message.Retain, message.Payload );

        public delegate ValueTask NewApplicationMessageHandlerDelegate( NewApplicationMessage applicationMessage, CancellationToken cancellationToken );

        class HandlerClosure
        {
            readonly NewApplicationMessageHandlerDelegate _messageHandler;
            public HandlerClosure( NewApplicationMessageHandlerDelegate messageHandler )
            {
                _messageHandler = messageHandler;
            }

            public ValueTask HandleMessage( string t, ReadOnlyMemory<byte> b, QualityOfService q, bool r, CancellationToken c )
                => _messageHandler( new NewApplicationMessage( t, b, q, r ), c );
        }

        public static void SetMessageHandler( this IMqtt3Client client, NewApplicationMessageHandlerDelegate handler )
            => client.SetMessageHandler( new HandlerClosure( handler ).HandleMessage );

        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory factory, MqttConfiguration config, NewApplicationMessageHandlerDelegate handler )
            => factory.CreateMQTT3Client( config, new HandlerClosure( handler ).HandleMessage );

        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory factory, MqttConfiguration config, NewApplicationMessageHandlerDelegate handler )
            => factory.CreateMQTT5Client( config, new HandlerClosure( handler ).HandleMessage );

        public static IMqttClient CreateMQTTClient( this MqttClientFactory factory, MqttConfiguration config, NewApplicationMessageHandlerDelegate handler )
            => factory.CreateMQTTClient( config, new HandlerClosure( handler ).HandleMessage );
    }
}
