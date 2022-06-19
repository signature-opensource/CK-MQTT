using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Packets
{
    public class SmallOutgoingApplicationMessage : OutgoingMessage
    {
        private readonly ReadOnlySequence<byte> _payload;

        public SmallOutgoingApplicationMessage( string topic, QualityOfService qos, bool retain, ReadOnlySequence<byte> payload,
            string? responseTopic = null, ushort correlationDataSize = 0, SpanAction? correlationDataWriter = null )//properties
            : base( topic, qos, retain, responseTopic, correlationDataSize, correlationDataWriter )
        {
            if( payload.Length > 268_435_455 ) throw new ArgumentException( "The buffer exceeed the max allowed size.", nameof( payload ) );
            _payload = payload;
        }

        public SmallOutgoingApplicationMessage( string topic, QualityOfService qos, bool retain, ReadOnlyMemory<byte> payload,
            string? responseTopic = null, ushort correlationDataSize = 0, SpanAction? correlationDataWriter = null )
            : this( topic, qos, retain, new ReadOnlySequence<byte>( payload ), responseTopic, correlationDataSize, correlationDataWriter )
        {
        }

        protected override uint PayloadSize => (uint)_payload.Length;

        public override ValueTask DisposeAsync() => new();

        protected override ValueTask WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            if( _payload.Length > 0 )
            {
                _payload.CopyTo( pw.GetSpan( (int)_payload.Length ) );
                pw.Advance( (int)_payload.Length );
            }
            return new ValueTask();
        }
    }
}
