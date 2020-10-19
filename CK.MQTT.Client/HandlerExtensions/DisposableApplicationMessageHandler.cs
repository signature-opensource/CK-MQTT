using CK.Core;
using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static CK.Core.Extension.PipeReaderExtensions;

namespace CK.MQTT
{
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

    public static class DisposableApplicationMessageExtensions
    {
        class HandlerClosure
        {
            readonly DisposableApplicationMessageHandlerDelegate _messageHandler;
            public HandlerClosure( DisposableApplicationMessageHandlerDelegate messageHandler )
            {
                _messageHandler = messageHandler;
            }

            public async ValueTask HandleMessage( string topic, PipeReader pipe, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
            {
                IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent( payloadLength );
                Memory<byte> buffer = memoryOwner.Memory[..payloadLength];
                FillStatus res = await pipe.FillBuffer( buffer, cancelToken );
                if( res != FillStatus.Done ) throw new EndOfStreamException();
                await _messageHandler( new DisposableApplicationMessage( topic, buffer, qos, retain, memoryOwner ), cancelToken );
            }
        }
        public static async ValueTask<Task> PublishAsync( this IMqtt3Client client, IActivityMonitor m, DisposableApplicationMessage message )
        {
            Task task = await client.PublishAsync( m, message.Topic, message.QoS, message.Retain, message.Payload );
            message.Dispose();
            return task;
        }

        public delegate ValueTask DisposableApplicationMessageHandlerDelegate( DisposableApplicationMessage applicationMessage, CancellationToken cancellationToken );

        public static void SetMessageHandler( this IMqtt3Client client, DisposableApplicationMessageHandlerDelegate handler )
            => client.SetMessageHandler( new HandlerClosure( handler ).HandleMessage );

        public static IMqtt3Client CreateMQTT3Client( this MqttClientFactory factory, MqttConfiguration config, DisposableApplicationMessageHandlerDelegate messageHandler )
            => factory.CreateMQTT3Client( config, new HandlerClosure( messageHandler ).HandleMessage );

        public static IMqtt5Client CreateMQTT5Client( this MqttClientFactory factory, MqttConfiguration config, DisposableApplicationMessageHandlerDelegate messageHandler )
            => factory.CreateMQTT5Client( config, new HandlerClosure( messageHandler ).HandleMessage );

        public static IMqttClient CreateMQTTClient( this MqttClientFactory factory, MqttConfiguration config, DisposableApplicationMessageHandlerDelegate messageHandler )
            => factory.CreateMQTTClient( config, new HandlerClosure( messageHandler ).HandleMessage );
    }
}
