using CK.Core;
using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static CK.Core.Extension.PipeReaderExtensions;

namespace CK.MQTT.Client.Closures
{
    public class BaseHandlerClosure
    {
        readonly Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> _messageHandler;
        public BaseHandlerClosure( Func<IActivityMonitor?, ApplicationMessage, CancellationToken, ValueTask> messageHandler )
            => _messageHandler = messageHandler;

        public async ValueTask HandleMessageAsync( IActivityMonitor? m,
            string topic, PipeReader pipe, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
        {
            Memory<byte> memory = new( new byte[payloadLength] );
            if( !memory.IsEmpty && await pipe.CopyToBufferAsync( memory, cancelToken ) != FillStatus.Done )
            {
                m?.Warn( "Partial data reading." );
                return;
            }
            await _messageHandler( m, new ApplicationMessage( topic, memory, qos, retain ), cancelToken );
        }
    }
}
