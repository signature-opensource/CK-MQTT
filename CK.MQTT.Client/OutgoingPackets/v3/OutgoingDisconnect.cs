using System;

namespace CK.MQTT
{
    /// <summary>
    /// Represent a disconnect packet to be serialized.
    /// </summary>
    public class OutgoingDisconnect : SimpleOutgoingPacket
    {
        private OutgoingDisconnect() { }

        /// <summary>
        /// Return the default instance of <see cref="OutgoingDisconnect"/>.
        /// </summary>
        public static OutgoingDisconnect Instance { get; } = new OutgoingDisconnect();

        /// <inheritdoc/>
        public override int GetSize( ProtocolLevel protocolLevel )
        {
            return 2;
        }

        /// <inheritdoc/>
        protected override void Write( ProtocolLevel protocolLevel, Span<byte> span)
		{
            span[0] = (byte)PacketType.Disconnect;
            span[1] = 0;
        }
    }
}
