using CK.Core;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static CK.Core.Extension.PipeReaderExtensions;

namespace CK.MQTT.Client
{
    public static class DisposableApplicationMessageExtensions
    {

        /// <param name="client">The client that will send the message.</param>
        /// <param name="m">The monitor used to log.</param>
        /// <param name="message">The message that will be sent. It will be disposed after being stored.</param>
        /// <returns>A ValueTask, that complete when the message has been stored, containing a Task that complete when the publish has been ack.
        /// On QoS 0, the message is directly sent and the returned Task is Task.CompletedTask.
        /// </returns>
        public static async ValueTask<Task> PublishAsync( this IMqtt3Client client, IActivityMonitor? m, DisposableApplicationMessage message )
        {
            Task task = await client.PublishAsync( m, message.Topic, message.QoS, message.Retain, message.Payload ); //The packet has been stored
            message.Dispose(); // So we can dispose after it has been stored.
            return task; // The task we return complete when the packet has been acked.
        }

        public static void SetMessageHandler( this IMqtt3Client client, Func<IActivityMonitor, DisposableApplicationMessage, CancellationToken, ValueTask> handler )
            => client.SetMessageHandler( new DisposableHandlerCancellableClosure( handler ).HandleMessage );

        public static void SetMessageHandler( this IMqtt3Client client, Func<IActivityMonitor, DisposableApplicationMessage, ValueTask> handler )
            => client.SetMessageHandler( new HandlerClosure( handler ).HandleMessage );


    }
    class DisposableHandlerCancellableClosure
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
    class HandlerClosure
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
    
}
