using System;

namespace CK.MQTT
{
    public class OutgoingDisconnect : SimpleOutgoingPacket
    {
        private OutgoingDisconnect() { }

        /// <summary>
        /// Return the default instance of <see cref="OutgoingDisconnect"/>.
        /// </summary>
        public static OutgoingDisconnect Instance { get; } = new OutgoingDisconnect();

        /// <inheritdoc/>
        public override int Size => 2;

        /// <inheritdoc/>
        protected override void Write( Span<byte> span )
        {
            span[0] = (byte)PacketType.Disconnect;
            span[1] = 0;
        }
    }
}
