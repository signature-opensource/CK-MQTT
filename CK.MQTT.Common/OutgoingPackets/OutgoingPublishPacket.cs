using CK.MQTT.Abstractions.Packets;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.OutgoingPackets
{
    public class SimpleOutgoingApplicationMessage : OutgoingApplicationMessage
    {
        readonly Func<int> _getPayloadSize;
        readonly Func<PipeWriter, CancellationToken, ValueTask> _payloadWriter;
        public SimpleOutgoingApplicationMessage(
            bool dup,
            bool retain,
            string topic,
            QualityOfService qos,
            Func<int> getPayloadSize,
            Func<PipeWriter, CancellationToken, ValueTask> payloadWriter
            ) : base( dup, retain, topic, qos )
        {
            _getPayloadSize = getPayloadSize;
            _payloadWriter = payloadWriter;
        }

        public override int GetSize() => HeaderSize + _getPayloadSize();

        protected override async ValueTask WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            await _payloadWriter( pw, cancellationToken );
            await pw.FlushAsync( cancellationToken );
        }
    }
}
