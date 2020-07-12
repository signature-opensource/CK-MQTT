using CK.Core;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static CK.MQTT.IOutgoingPacket;

namespace CK.MQTT
{
    public abstract class SimpleOutgoingPacket : IOutgoingPacket
    {
        protected abstract void Write( Span<byte> buffer );

        void Write( PipeWriter pw )
        {
            Write( pw.GetSpan( Size ) );
            pw.Advance( Size );
        }

        public async ValueTask<WriteResult> WriteAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            Write( pw );
            await pw.FlushAsync( cancellationToken ).AsNonGenericValueTask();
            return WriteResult.Written;
        }

        public abstract int Size { get; }
    }
}
