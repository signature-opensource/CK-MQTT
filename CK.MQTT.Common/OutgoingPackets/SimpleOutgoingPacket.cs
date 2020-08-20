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
        /// <summary>
        /// Allow to write synchronously to the input buffer.
        /// </summary>
        /// <param name="buffer">The buffer to modify.</param>
        protected abstract void Write( Span<byte> buffer );

        void Write( PipeWriter pw )
        {
            Write( pw.GetSpan( Size ) );
            pw.Advance( Size );
        }

        /// <inheritdoc/>
        public async ValueTask<WriteResult> WriteAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            Write( pw );
            await pw.FlushAsync( cancellationToken ).AsNonGenericValueTask();
            return WriteResult.Written;
        }

        /// <inheritdoc/>
        public abstract int Size { get; }
    }
}
