using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static CK.MQTT.IOutgoingPacket;

namespace CK.MQTT
{
    public class SimpleOutgoingApplicationMessage : OutgoingMessage
    {
        readonly Func<int> _getPayloadSize;
        readonly Func<PipeWriter, CancellationToken, ValueTask<WriteResult>> _payloadWriter;
        public SimpleOutgoingApplicationMessage( bool retain, string topic, QualityOfService qos, Func<int> getPayloadSize,
            Func<PipeWriter, CancellationToken, ValueTask<WriteResult>> payloadWriter
            ) : base( retain, topic, qos )
        {
            _getPayloadSize = getPayloadSize;
            _payloadWriter = payloadWriter;
        }

        protected override int PayloadSize => _getPayloadSize();

        protected override ValueTask<WriteResult> WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken )
            => _payloadWriter( pw, cancellationToken );
    }
}
