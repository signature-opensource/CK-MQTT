using CK.Core;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.ExtensionMethods.Closures
{
    public class LogHandlerClosure
    {
        readonly Func<IActivityMonitor?, string, uint, CancellationToken, ValueTask> _messageHandler;
        public LogHandlerClosure( Func<IActivityMonitor?, string, uint, CancellationToken, ValueTask> messageHandler )
            => _messageHandler = messageHandler;

        public async ValueTask HandleMessageAsync( IActivityMonitor? m, string topic, PipeReader pipe, uint payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
        {
            uint remaining = payloadLength;
            while( remaining > 0 )
            {
                ReadResult result = await pipe.ReadAsync( cancelToken );
                ReadOnlySequence<byte> buffer = result.Buffer;
                if( buffer.Length > remaining ) buffer = buffer.Slice( 0, remaining );
                pipe.AdvanceTo( buffer.End );
                remaining -= (uint)buffer.Length;
            }
            await _messageHandler( m, topic, payloadLength, cancelToken);
        }
    }
}
