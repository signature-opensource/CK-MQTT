using System;

namespace CK.MQTT
{
    /// <summary>
    /// Simplify the serialisation of a variable size small packet.
    /// </summary>
    public abstract class VariableOutgointPacket : SimpleOutgoingPacket
    {
        protected abstract byte Header { get; }

        protected abstract int GetRemainingSize( ProtocolLevel protocolLevel );

        /// <inheritdoc/>
        public override int GetSize( ProtocolLevel protocolLevel ) => GetRemainingSize( protocolLevel ).CompactByteCount() + 1 + GetRemainingSize( protocolLevel );

        protected abstract void WriteContent( ProtocolLevel protocolLevel, Span<byte> buffer);

        /// <inheritdoc/>
        protected override void Write( ProtocolLevel protocolLevel, Span<byte> span)
		{
            span[0] = Header;
            span = span[1..].WriteVariableByteInteger( GetRemainingSize( protocolLevel ) );
            WriteContent( protocolLevel, span );
        }
    }
}
