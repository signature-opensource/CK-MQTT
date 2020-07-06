using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public abstract class ComplexOutgoingPacket : IOutgoingPacket
    {
        protected abstract byte Header { get; }

        public int Size => RemainingSize + 1 + RemainingSize.CompactByteCount();

        int RemainingSize => HeaderSize + PayloadSize;

        protected abstract int PayloadSize { get; }

        protected abstract int HeaderSize { get; }

        protected void WriteHeader( PipeWriter pw )
        {
            int remainingSize = RemainingSize;
            int sizeOfSize = remainingSize.CompactByteCount();
            int bytesToWrite = HeaderSize + sizeOfSize + 1;
            Span<byte> span = pw.GetSpan( bytesToWrite );
            span[0] = Header;
            span = span[1..].WriteRemainingSize( remainingSize );
            WriteHeaderContent( span[..HeaderSize] );
            pw.Advance( bytesToWrite );
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
