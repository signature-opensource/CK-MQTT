using CK.Core;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static CK.MQTT.IOutgoingPacket;

namespace CK.MQTT
{
    public class SmallOutgoingApplicationMessage : OutgoingMessage
    {
        private readonly ReadOnlyMemory<byte> _memory;

        public SmallOutgoingApplicationMessage( bool retain, string topic, QualityOfService qos, ReadOnlyMemory<byte> payload )
            : base( retain, topic, qos )
        {
            _memory = payload;
        }

        protected override int PayloadSize => _memory.Length;

        protected async override ValueTask<WriteResult> WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            await pw.WriteAsync( _memory ).AsNonGenericValueTask();
            return WriteResult.Written;
        }
    }
}
