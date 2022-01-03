using CK.Core;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Closures
{
    public class BaseHandlerClosure
    {
        readonly Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> _messageHandler;
        public BaseHandlerClosure( Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => _messageHandler = messageHandler;

        public async ValueTask HandleMessageAsync( IActivityMonitor? m,
            string topic, PipeReader pipe, uint payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
        {
            Memory<byte> memory = new( new byte[payloadLength] );
            if( !memory.IsEmpty )
            {
                ReadResult readResult = await pipe.ReadAtLeastAsync( (int)payloadLength, cancelToken );
                if( readResult.IsCanceled || readResult.IsCompleted && readResult.Buffer.Length < memory.Length )
                {
                    m?.Warn( "Partial data reading." );
                    pipe.AdvanceTo( readResult.Buffer.Slice( Math.Min( memory.Length, readResult.Buffer.Length ) ).End );
                    return;
                }
                ReadOnlySequence<byte> sliced = readResult.Buffer.Slice( 0, memory.Length );
                sliced.CopyTo( memory.Span );
                pipe.AdvanceTo( sliced.End );

            }
            await _messageHandler( m, new ApplicationMessage( topic, memory, qos, retain ), cancelToken );
        }
    }
}
