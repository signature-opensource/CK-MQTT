using CK.MQTT.Packets;
using System;

namespace CK.MQTT.Server.OutgoingPackets
{
    class OutgoingPingResp : SimpleOutgoingPacket
    {
        private OutgoingPingResp() { }
        /// <summary>
        /// Return the default instance of <see cref="OutgoingPingReq"/>.
        /// </summary>
        public static OutgoingPingResp Instance { get; } = new OutgoingPingResp();
        public override ushort PacketId { get => 0; set => throw new NotSupportedException(); }
        public override bool IsRemoteOwnedPacketId => throw new NotSupportedException();
        public override PacketType Type => PacketType.PingResponse;

        public override QualityOfService Qos => QualityOfService.AtMostOnce;

        public override uint GetSize( ProtocolLevel protocolLevel ) => 2;

        protected override void Write( ProtocolLevel protocolLevel, Span<byte> span )
        {
            span[0] = (byte)PacketType.PingResponse;
            span[1] = 0;
        }
    }
}
