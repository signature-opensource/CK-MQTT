using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// An <see cref="IOutgoingPacket"/> that have a variable header writable synchronously, and a variable payload writable asynchronously.
    /// </summary>
    public abstract class ComplexOutgoingPacket : IOutgoingPacket
    {
        /// <summary>
        /// The first byte of the packet. This contain the <see cref="PacketType"/> and possibly other data.
        /// </summary>
        protected abstract byte Header { get; }

        /// <inheritdoc/>
        public int Size => RemainingSize + 1 + RemainingSize.CompactByteCount();

        /// <summary>
        /// The <see cref="Size"/>, minus the header, and the bytes required to write the <see cref="RemainingSize"/> itself.
        /// </summary>
        int RemainingSize => HeaderSize + PayloadSize;

        /// <summary>
        /// The size of the Payload to write asynchronously.
        /// This is the amount of bytes that MUST be written when <see cref="WritePayloadAsync(PipeWriter, CancellationToken)"/> will be called.
        /// </summary>
        protected abstract int PayloadSize { get; }

        /// <summary>
        /// The size of the Header.
        /// This is also the size of the <see cref="Span{T}"/> given when <see cref="WriteHeaderContent(Span{byte})"/> will be called.
        /// </summary>
        protected abstract int HeaderSize { get; }

        /// <summary>
        /// Write the Header, remaining size, and call <see cref="WriteHeaderContent(Span{byte})"/>.
        /// </summary>
        /// <param name="pw"></param>
        protected void WriteHeader( PipeWriter pw )
        {
            int bytesToWrite = HeaderSize + RemainingSize.CompactByteCount() + 1;//Compute how many byte will be written.
            Span<byte> span = pw.GetSpan( bytesToWrite );
            span[0] = Header;
            span = span[1..].WriteRemainingSize( RemainingSize );//the result span length will be HeaderSize.
            WriteHeaderContent( span[..HeaderSize] );
            pw.Advance( bytesToWrite );//advance the number of bytes written.
        }

        protected abstract void WriteHeaderContent( Span<byte> span );

        protected abstract ValueTask<bool> WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken );

        public ValueTask<bool> WriteAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            WriteHeader( pw );
            return WritePayloadAsync( pw, cancellationToken );
        }
    }
}
