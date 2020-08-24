using System;

namespace CK.MQTT
{
    /// <summary>
    /// Simplify the serialisation of a variable size small packet.
    /// </summary>
    public abstract class VariableOutgointPacket : SimpleOutgoingPacket
    {
        protected abstract byte Header { get; }

        protected abstract int RemainingSize { get; }

        /// <inheritdoc/>
        public override int Size => RemainingSize.CompactByteCount() + 1 + RemainingSize;

        protected abstract void WriteContent( Span<byte> buffer );

        /// <inheritdoc/>
        protected override void Write( Span<byte> span )
        {
            span[0] = Header;
            span = span[1..].WriteVariableByteInteger( RemainingSize );
            WriteContent( span );
        }
    }
}
