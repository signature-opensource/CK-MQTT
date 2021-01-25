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
        /// <param name="protocolLevel"></param>
        /// <param name="buffer">The buffer to modify.</param>
        protected abstract void Write( ProtocolLevel protocolLevel, Span<byte> buffer );

        void Write( ProtocolLevel protocolLevel, PipeWriter pw )
        {
            Write( protocolLevel, pw.GetSpan( GetSize( protocolLevel ) ) );
            pw.Advance( GetSize( protocolLevel ) );
        }

        /// <inheritdoc/>
        public async ValueTask<WriteResult> WriteAsync( ProtocolLevel protocolLevel, PipeWriter pw, CancellationToken cancellationToken )
        {
            Write( protocolLevel, pw );
            await pw.FlushAsync( cancellationToken ).AsNonGenericValueTask();
            return WriteResult.Written;
        }

        /// <inheritdoc/>
        public abstract int GetSize( ProtocolLevel protocolLevel );
    }
}
