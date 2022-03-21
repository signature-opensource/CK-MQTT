using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Packets
{
    public delegate ValueTask<WriteResult> PayloadWriterDelegate( PipeWriter writer, CancellationToken cancellationToken );
    class BasicOutgoingApplicationMessage : OutgoingMessage
    {
        readonly Func<uint> _getPayloadSize;
        readonly PayloadWriterDelegate _payloadWriter;

        public BasicOutgoingApplicationMessage(
            string topic, QualityOfService qos, bool retain, Func<uint> getPayloadSize, PayloadWriterDelegate payloadWriter,
            string? responseTopic = null, ushort correlationDataSize = 0, SpanAction? correlationDataWriter = null ) //Properties
             : base( topic, qos, retain, responseTopic, correlationDataSize, correlationDataWriter )
        {
            _getPayloadSize = getPayloadSize;
            _payloadWriter = payloadWriter;
        }

        protected override uint PayloadSize => _getPayloadSize();

        protected override ValueTask<WriteResult> WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken )
            => _payloadWriter( pw, cancellationToken );
    }
}
