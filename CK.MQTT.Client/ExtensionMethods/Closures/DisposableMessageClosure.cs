using CK.Core;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static CK.Core.Extension.PipeReaderExtensions;

namespace CK.MQTT.Client.Closures
{
    public class DisposableMessageClosure
    {
        readonly Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> _messageHandler;
        public DisposableMessageClosure( Func<IActivityMonitor?, DisposableApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => _messageHandler = messageHandler;

        public async ValueTask HandleMessageAsync( IActivityMonitor? m, string topic, PipeReader pipe, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
        {
            IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent( payloadLength );
            Memory<byte> buffer = memoryOwner.Memory[..payloadLength];
            if( await pipe.CopyToBufferAsync( buffer, cancelToken ) != FillStatus.Done ) Debug.Fail( "Unexpected partial read." );
            await _messageHandler( m, new DisposableApplicationMessage( topic, buffer, qos, retain, memoryOwner ), cancelToken );
        }
    }
}
