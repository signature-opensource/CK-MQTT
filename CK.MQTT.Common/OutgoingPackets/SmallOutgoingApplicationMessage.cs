using CK.Core;
using CK.MQTT.Abstractions.Packets;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.OutgoingPackets
{
    public class SmallOutgoingApplicationMessage : OutgoingApplicationMessage
    {
        private readonly ReadOnlyMemory<byte> _memory;

        public SmallOutgoingApplicationMessage(bool dup, bool retain, string topic, QualityOfService qos, ReadOnlyMemory<byte> payload)
            : base(dup, retain, topic, qos )
        {
            _memory = payload;
        }
        public override int GetSize() => _memory.Length;

        protected override ValueTask WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken )
            => pw.WriteAsync( _memory ).AsNonGenericValueTask();
    }
}
