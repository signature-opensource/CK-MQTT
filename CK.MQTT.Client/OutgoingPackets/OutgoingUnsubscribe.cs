using CK.MQTT.Common;
using CK.MQTT.Common.OutgoingPackets;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.MQTT.Client.OutgoingPackets
{
    class OutgoingUnsubscribe : VariableOutgointPacket
    {
        private readonly ushort _packetId;
        private readonly IEnumerable<string> _topics;

        public OutgoingUnsubscribe( ushort packetId, IEnumerable<string> topics )
        {
            _packetId = packetId;
            _topics = topics;
        }
        //The bit set is caused by MQTT-3.8.1-1: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc442180829
        protected override byte Header => (byte)((byte)PacketType | 0b0000_0010);

        protected override int RemainingSize => 2 + _topics.Sum( s => s.MQTTSize() );

        protected override PacketType PacketType => PacketType.Unsubscribe;

        protected override void WriteContent( Span<byte> span )
        {
            span = span.WriteUInt16( _packetId );
            foreach( string topic in _topics )
            {
                span = span.WriteString( topic );
            }
        }
    }
}
