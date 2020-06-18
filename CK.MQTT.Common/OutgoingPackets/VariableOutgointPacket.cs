using CK.MQTT.Common.Serialisation;
using System;
using System.IO.Pipelines;

namespace CK.MQTT.Common.OutgoingPackets
{
    /// <summary>
    /// Simplify the serialisation of a variable size small packet.
    /// </summary>
    public abstract class VariableOutgointPacket : SimpleOutgoingPacket
    {
        protected abstract byte Header { get; }

        protected abstract int RemainingSize { get; }

        public override int GetSize() => RemainingSize.CompactByteCount() + 1 + RemainingSize;

        protected abstract void WriteContent( Span<byte> buffer );

        protected override void Write( PipeWriter pw )
        {
            int remainingSize = RemainingSize;
            int sizeOfSize = remainingSize.CompactByteCount();
            int bytesToWrite = GetSize();
            Span<byte> span = pw.GetSpan( bytesToWrite );
            span[0] = Header;
            span = span[1..].WriteRemainingSize( remainingSize );
            WriteContent( span );
            pw.Advance( bytesToWrite );
        }
    }
}
