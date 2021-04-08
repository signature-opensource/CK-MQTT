using System;
using System.Buffers.Binary;

namespace CK.MQTT
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

        /// <inheritdoc/>
        protected override byte Header => (byte)PacketType.SubscribeAck;

        /// <inheritdoc/>
        protected override int GetRemainingSize( ProtocolLevel protocolLevel )
        {
            return 2 + _returnCodes.Length;
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
