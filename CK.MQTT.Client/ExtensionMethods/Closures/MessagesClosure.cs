using CK.Core;
using CK.MQTT.Client;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static CK.Core.Extension.PipeReaderExtensions;

namespace CK.MQTT.Client.Closures
{
    public class MessagesClosure
    {
        readonly Func<IActivityMonitor?, ApplicationMessage, ValueTask> _messageHandler;
        public MessagesClosure( Func<IActivityMonitor?, ApplicationMessage, ValueTask> messageHandler )
            => _messageHandler = messageHandler;

        public async ValueTask HandleMessageAsync( IActivityMonitor? m,
            string topic, PipeReader pipe, int payloadLength, QualityOfService qos, bool retain, CancellationToken cancelToken )
        {
            Memory<byte> memory = new( new byte[payloadLength] );
            FillStatus status = await pipe.CopyToBufferAsync( memory, cancelToken );
            if( status != FillStatus.Done ) throw new InvalidOperationException( "Unexpected partial read." );
            await _messageHandler( m, new ApplicationMessage( topic, memory, qos, retain ) );
        }
    }
}
