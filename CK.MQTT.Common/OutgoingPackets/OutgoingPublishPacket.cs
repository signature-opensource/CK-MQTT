using CK.MQTT.Abstractions.Packets;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
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
            Func<PipeWriter, CancellationToken, ValueTask> payloadWriter,
            ushort packetId = 0
            ) : base(dup, retain, topic, qos, packetId)
        {
            _getPayloadSize = getPayloadSize;
            _payloadWriter = payloadWriter;
        }

        protected override int RemainingSize => HeaderSize + _getPayloadSize();

        protected override async ValueTask WriteRestOfThePacketAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            await _payloadWriter( pw, cancellationToken );
            await pw.FlushAsync( cancellationToken );
        }
    }
}
