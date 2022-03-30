using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Packets
{
    public class SmallOutgoingApplicationMessage : OutgoingMessage
    {
        private readonly ReadOnlyMemory<byte> _memory;

        public SmallOutgoingApplicationMessage( string topic, QualityOfService qos, bool retain, ReadOnlyMemory<byte> payload,
            string? responseTopic = null, ushort correlationDataSize = 0, SpanAction? correlationDataWriter = null )//properties
            : base( topic, qos, retain, responseTopic, correlationDataSize, correlationDataWriter )
        {
            _memory = payload;
        }

        protected override uint PayloadSize => (uint)_memory.Length;

        protected async override ValueTask<WriteResult> WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            await pw.WriteAsync( _memory, cancellationToken );
            return WriteResult.Written;
        }
    }
}
