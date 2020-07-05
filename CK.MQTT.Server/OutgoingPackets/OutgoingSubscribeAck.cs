using System;

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

        protected override byte Header => (byte)PacketType.SubscribeAck;

        protected override int RemainingSize => 2 + _returnCodes.Length;

        protected override void WriteContent( Span<byte> span )
        {
            span = span.WriteUInt16( _packetId );
            for( int i = 0; i < _returnCodes.Length; i++ )
            {
                span[i] = (byte)_returnCodes[i];
            }
        }
    }
}
