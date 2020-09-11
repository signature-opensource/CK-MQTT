using CK.Core;
using CK.MQTT.Client.HandlerExtensions;
using System;
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

        public static void SetMessageHandler( this IMqtt3Client client, NewApplicationMessageHandlerDelegate handler )
            => client.SetMessageHandler( ( t, p, q, r, c ) => handler( new NewApplicationMessage( t, p, q, r ), c ) );
    }

    public class DisposableApplicationMessage : IDisposable
    {
        readonly IDisposable _disposable;

        public DisposableApplicationMessage( string topic, ReadOnlyMemory<byte> payload, QualityOfService qoS, bool retain, IDisposable disposable )
        {
            Topic = topic;
            Payload = payload;
            QoS = qoS;
            Retain = retain;
            _disposable = disposable;
        }
        public string Topic { get; }
        public ReadOnlyMemory<byte> Payload { get; }
        public QualityOfService QoS { get; }
        public bool Retain { get; }

        public void Dispose() => _disposable.Dispose();
    }
}
