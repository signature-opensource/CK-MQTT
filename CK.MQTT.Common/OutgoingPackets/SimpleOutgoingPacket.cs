using CK.Core;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common
{
    public abstract class SimpleOutgoingPacket : IOutgoingPacket
    {
        protected abstract void Write( Span<byte> buffer );

        void Write( PipeWriter pw )
        {
            Write( pw.GetSpan( Size ) );
            pw.Advance( Size );
        }

        public ValueTask WriteAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            Write( pw );
            return pw.FlushAsync( cancellationToken ).AsNonGenericValueTask();
        }

        public abstract int Size { get; }

        public bool Burned => false;
    }
}
