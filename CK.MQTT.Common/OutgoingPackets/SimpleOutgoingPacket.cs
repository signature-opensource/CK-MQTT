using CK.Core;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

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

        public async ValueTask<bool> WriteAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            Write( pw );
            await pw.FlushAsync( cancellationToken ).AsNonGenericValueTask();
            return false;
        }

        public abstract int Size { get; }

        public bool Burned => false;
    }
}
