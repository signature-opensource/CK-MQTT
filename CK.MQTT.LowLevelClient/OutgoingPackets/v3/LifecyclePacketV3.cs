using CK.MQTT.Packets;
using System;
using System.Buffers.Binary;

namespace CK.MQTT
{
    public sealed class LifecyclePacketV3 : SimpleOutgoingPacket, IOutgoingPacket
    {
        readonly byte _header;

        public override bool IsRemoteOwnedPacketId { get; }

        public override ushort PacketId { get; set; }

        public override QualityOfService Qos => QualityOfService.AtLeastOnce;

        public LifecyclePacketV3( byte header, ushort packetId, bool isRemoteOwnedPacketId )
        {
            _header = header;
            PacketId = packetId;
            IsRemoteOwnedPacketId = isRemoteOwnedPacketId;
        }

        /// <inheritdoc/>
        public override uint GetSize( ProtocolLevel protocolLevel ) => 4;

        /// <inheritdoc/>
        protected override void Write( ProtocolLevel protocolLevel, Span<byte> span )
        {
            span[0] = _header;
            span[1] = 2;
            span = span[2..];
            BinaryPrimitives.WriteUInt16BigEndian( span, (ushort)PacketId );
        }

        public static IOutgoingPacket Pubrel( ushort packetId ) => new LifecyclePacketV3( (byte)PacketType.PublishRelease | 0b0010, packetId, false );
        public static IOutgoingPacket Pubrec( ushort packetId ) => new LifecyclePacketV3( (byte)PacketType.PublishReceived, packetId, true );
        public static IOutgoingPacket Pubcomp( ushort packetId ) => new LifecyclePacketV3( (byte)PacketType.PublishComplete, packetId, true );
        public static IOutgoingPacket Puback( ushort packetId ) => new LifecyclePacketV3( (byte)PacketType.PublishAck, packetId, true );

        public override string ToString() => ((PacketType)(_header ^ 0b0010)).ToString();
    }
}
