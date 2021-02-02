using CK.Core;
using System;
using System.Buffers;
using System.Diagnostics;
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
            => (Topic, Payload, QoS, Retain, _disposable) = (topic, payload, qoS, retain, disposable);

        public string Topic { get; }
        public ReadOnlyMemory<byte> Payload { get; }
        public QualityOfService QoS { get; }
        public bool Retain { get; }
        public void Dispose() => _disposable.Dispose();
    }
    internal class DisposableHandlerCancellableClosure
    {
        readonly Func<IActivityMonitor, DisposableApplicationMessage, CancellationToken, ValueTask> _messageHandler;
        public DisposableHandlerCancellableClosure( Func<IActivityMonitor, DisposableApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => _messageHandler = messageHandler;

        public async ValueTask HandleMessage( IActivityMonitor m, string topic, PipeReader pipe, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
        {
            IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent( payloadLength );
            Memory<byte> buffer = memoryOwner.Memory[..payloadLength];
            if( await pipe.CopyToBuffer( buffer, cancelToken ) != FillStatus.Done ) Debug.Fail( "Unexpected partial read." );
            await _messageHandler( m, new DisposableApplicationMessage( topic, buffer, qos, retain, memoryOwner ), cancelToken );
        }
    }
    internal class HandlerClosure
    {
        readonly Func<IActivityMonitor, DisposableApplicationMessage, ValueTask> _messageHandler;
        public HandlerClosure( Func<IActivityMonitor, DisposableApplicationMessage, ValueTask> messageHandler )
            => _messageHandler = messageHandler;

        public async ValueTask HandleMessage( IActivityMonitor m, string topic, PipeReader pipe, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
        {
            IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent( payloadLength );
            Memory<byte> buffer = memoryOwner.Memory[..payloadLength];
            FillStatus res = await pipe.CopyToBuffer( buffer, cancelToken );
            if( res != FillStatus.Done ) throw new EndOfStreamException();
            await _messageHandler( m, new DisposableApplicationMessage( topic, buffer, qos, retain, memoryOwner ) );
        }
    }
    public static class DisposableApplicationMessageExtensions
    {
        
        public static async ValueTask<Task> PublishAsync( this IMqtt3Client client, IActivityMonitor m, DisposableApplicationMessage message )
        {
            Task task = await client.PublishAsync( m, message.Topic, message.QoS, message.Retain, message.Payload );
            message.Dispose();
            return task;
        }

        public static void SetMessageHandler( this IMqtt3Client client, Func<IActivityMonitor, DisposableApplicationMessage, CancellationToken, ValueTask> handler )
            => client.SetMessageHandler( new DisposableHandlerCancellableClosure( handler ).HandleMessage );

        public static void SetMessageHandler( this IMqtt3Client client, Func<IActivityMonitor, DisposableApplicationMessage, ValueTask> handler )
            => client.SetMessageHandler( new HandlerClosure( handler ).HandleMessage );

        
    }
}
