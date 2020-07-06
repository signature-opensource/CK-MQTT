using CK.Core;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class SmallOutgoingApplicationMessage : OutgoingApplicationMessage
    {
        private readonly ReadOnlyMemory<byte> _memory;

        public SmallOutgoingApplicationMessage( bool dup, bool retain, string topic, QualityOfService qos, ReadOnlyMemory<byte> payload )
            : base( dup, retain, topic, qos )
        {
            _memory = payload;
        }
        protected override int PayloadSize => _memory.Length;

        protected async override ValueTask<bool> WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            await pw.WriteAsync( _memory ).AsNonGenericValueTask();
            return false;
        }
    }
}
