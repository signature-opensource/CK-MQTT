using CK.MQTT.Packets;
using System;
using System.Buffers.Binary;

namespace CK.MQTT.Server.OutgoingPackets
{
    class OutgoingSubscribeAck : VariableOutgointPacket
    {
        readonly ushort _packetId;
        readonly SubscribeReturnCode[] _returnCodes;

        public OutgoingSubscribeAck( ushort packetId, SubscribeReturnCode[] returnCodes )
        {
            _packetId = packetId;
            _returnCodes = returnCodes;
        }

        public override ushort PacketId { get => 0; set => throw new NotSupportedException(); }

        public override QualityOfService Qos => QualityOfService.AtMostOnce;

        public override bool IsRemoteOwnedPacketId => true;

        public override PacketType Type => PacketType.Subscribe;

        /// <inheritdoc/>
        protected override byte Header => (byte)PacketType.SubscribeAck;

        /// <inheritdoc/>
        protected override uint GetRemainingSize( ProtocolLevel protocolLevel )
        {
            return 2 + (uint)_returnCodes.Length;
        }

        /// <inheritdoc/>
        protected override void WriteContent( ProtocolLevel protocolLevel, Span<byte> span )
        {
            BinaryPrimitives.WriteUInt16BigEndian( span, _packetId );
            span = span[2..];
            for( int i = 0; i < _returnCodes.Length; i++ )
            {
                span[i] = (byte)_returnCodes[i];
            }
        }
    }
}
