using CK.Core;
using CK.MQTT.Client.HandlerExtensions;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static CK.Core.Extension.PipeReaderExtensions;

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

    public static class DisposableApplicationMessageExtensions
    {
        public static async ValueTask<Task> PublishAsync( this IMqtt3Client client, IActivityMonitor m, DisposableApplicationMessage message )
        {
            Task task = await client.PublishAsync( m, message.Topic, message.QoS, message.Retain, message.Payload );
            message.Dispose();
            return task;
        }

        public delegate ValueTask DisposableApplicationMessageHandlerDelegate( DisposableApplicationMessage applicationMessage, CancellationToken cancellationToken );

        public static void SetMessageHandler( this IMqtt3Client client, DisposableApplicationMessageHandlerDelegate handler )
            => client.SetMessageHandler( async ( topic, pipeReader, payloadLength, qos, retain, cancellationToken ) =>
            {
                IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent( payloadLength );
                Memory<byte> buffer = memoryOwner.Memory[..payloadLength];
                FillStatus res = await pipeReader.FillBuffer( buffer, cancellationToken );
                if( res != FillStatus.Done ) throw new EndOfStreamException();
                await handler( new DisposableApplicationMessage( topic, buffer, qos, retain, memoryOwner ), cancellationToken );
            } );
    }
}
