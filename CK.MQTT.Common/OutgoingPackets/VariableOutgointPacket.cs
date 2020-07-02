using CK.MQTT.Common.Serialisation;
using System;

namespace CK.MQTT.Common.OutgoingPackets
{
    /// <summary>
    /// Simplify the serialisation of a variable size small packet.
    /// </summary>
    public abstract class VariableOutgointPacket : SimpleOutgoingPacket
    {
        protected abstract byte Header { get; }

        protected abstract int RemainingSize { get; }

        public override int Size => RemainingSize.CompactByteCount() + 1 + RemainingSize;

        protected abstract void WriteContent( Span<byte> buffer );

        protected override void Write( Span<byte> span )
        {
            span[0] = Header;
            span = span[1..].WriteRemainingSize( RemainingSize );
            WriteContent( span );
        }
    }
}
