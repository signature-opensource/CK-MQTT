using CK.MQTT.Packets;
using System;

namespace CK.MQTT
{
    class OutgoingPingReq : SimpleOutgoingPacket
    {
        private OutgoingPingReq() { }
        /// <summary>
        /// Return the default instance of <see cref="OutgoingPingReq"/>.
        /// </summary>
        public static OutgoingPingReq Instance { get; } = new OutgoingPingReq();
        public override ushort PacketId { get => 0; set => throw new NotSupportedException(); }
        public override bool IsRemoteOwnedPacketId => throw new NotSupportedException();
        public override PacketType Type => PacketType.PingRequest;

        public override QualityOfService Qos => QualityOfService.AtMostOnce;

        public override uint GetSize( ProtocolLevel protocolLevel )
        {
            return 2;
        }

        protected override void Write( ProtocolLevel protocolLevel, Span<byte> span )
        {
            span[0] = (byte)PacketType.PingRequest;
            span[1] = 0;
        }
    }
}
