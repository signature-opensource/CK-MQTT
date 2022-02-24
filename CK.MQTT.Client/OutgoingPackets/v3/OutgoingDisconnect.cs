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
        public override uint PacketId { get => 0; set => throw new NotSupportedException(); }

        public override QualityOfService Qos => QualityOfService.AtMostOnce;

        public override bool IsRemoteOwnedPacketId => throw new NotSupportedException();

        /// <inheritdoc/>
        public override uint GetSize( ProtocolLevel protocolLevel ) => 2;

        /// <inheritdoc/>
        protected override void Write( ProtocolLevel protocolLevel, Span<byte> span )
        {
            span[0] = (byte)PacketType.Disconnect;
            span[1] = 0;
        }
    }
}
