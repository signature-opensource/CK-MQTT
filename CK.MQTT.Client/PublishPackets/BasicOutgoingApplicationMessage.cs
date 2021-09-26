using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static CK.MQTT.IOutgoingPacket;

namespace CK.MQTT
{
    public delegate ValueTask<WriteResult> PayloadWriterDelegate( PipeWriter writer, CancellationToken cancellationToken );
    class BasicOutgoingApplicationMessage : OutgoingMessage
    {
        readonly Func<int> _getPayloadSize;
        readonly PayloadWriterDelegate _payloadWriter;

        public BasicOutgoingApplicationMessage(
            string topic, QualityOfService qos, bool retain, Func<int> getPayloadSize, PayloadWriterDelegate payloadWriter,
            string? responseTopic = null, ushort correlationDataSize = 0, SpanAction? correlationDataWriter = null ) //Properties
             : base( topic, qos, retain, responseTopic, correlationDataSize, correlationDataWriter )
        {
            _getPayloadSize = getPayloadSize;
            _payloadWriter = payloadWriter;
        }

        protected override int PayloadSize => _getPayloadSize();

        protected override ValueTask<WriteResult> WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken )
            => _payloadWriter( pw, cancellationToken );
    }
}
