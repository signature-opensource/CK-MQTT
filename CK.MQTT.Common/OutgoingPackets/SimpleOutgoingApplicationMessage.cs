using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common
{
    public class SimpleOutgoingApplicationMessage : OutgoingApplicationMessage
    {
        readonly Func<int> _getPayloadSize;
        readonly Func<PipeWriter, CancellationToken, ValueTask> _payloadWriter;
        readonly bool _writeOnce;
        bool _burned;
        public SimpleOutgoingApplicationMessage(
            bool dup,
            bool retain,
            string topic,
            QualityOfService qos,
            Func<int> getPayloadSize,
            Func<PipeWriter, CancellationToken, ValueTask> payloadWriter,
            bool writeOnce
            ) : base( dup, retain, topic, qos )
        {
            _getPayloadSize = getPayloadSize;
            _payloadWriter = payloadWriter;
            _writeOnce = writeOnce;
        }
        protected override int PayloadSize => _getPayloadSize();

        public override bool Burned => _burned;

        protected override async ValueTask WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            if( _writeOnce ) _burned = true;
            await _payloadWriter( pw, cancellationToken );
            await pw.FlushAsync( cancellationToken );
        }
    }
}
