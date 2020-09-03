using System;

namespace CK.MQTT
{
    public sealed class LifecyclePacketV3 : SimpleOutgoingPacket, IOutgoingPacketWithId
    {
        readonly byte _header;
        public int PacketId { get; set; }

        public QualityOfService Qos => QualityOfService.AtLeastOnce;

        public LifecyclePacketV3( byte header, int packetId )
        {
            _header = header;
            PacketId = packetId;
        }

        /// <inheritdoc/>
        public override int GetSize( ProtocolLevel protocolLevel ) => 4;

        /// <inheritdoc/>
        protected override void Write( Span<byte> span )
        {
            span[0] = _header;
            span[1] = 2;
            span[2..].WriteUInt16( (ushort)PacketId );
        }

        public static IOutgoingPacketWithId Pubrel( int packetId ) => new LifecyclePacketV3( (byte)PacketType.PublishRelease | 0b0010, packetId );
        public static IOutgoingPacketWithId Pubrec( int packetId ) => new LifecyclePacketV3( (byte)PacketType.PublishReceived, packetId );
        public static IOutgoingPacketWithId Pubcomp( int packetId ) => new LifecyclePacketV3( (byte)PacketType.PublishComplete, packetId );
        public static IOutgoingPacketWithId Puback( int packetId ) => new LifecyclePacketV3( (byte)PacketType.PublishAck, packetId );
    }
}
