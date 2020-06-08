using CK.MQTT.Common;
using CK.MQTT.Common.OutgoingPackets;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;

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

        protected override PacketType PacketType => PacketType.SubscribeAck;

        protected override byte Header => (byte)PacketType;

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
