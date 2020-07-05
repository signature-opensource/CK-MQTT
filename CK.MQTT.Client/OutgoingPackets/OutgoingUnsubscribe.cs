using System;
using System.Linq;

namespace CK.MQTT.Client
{
    class OutgoingUnsubscribe : VariableOutgointPacket, IOutgoingPacketWithId
    {
        private readonly string[] _topics;

        public OutgoingUnsubscribe( string[] topics )
        {
            _topics = topics;
        }

        public int PacketId { get; set; }

        public QualityOfService Qos => QualityOfService.AtLeastOnce;

        //The bit set is caused by MQTT-3.8.1-1: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc442180829
        protected override byte Header => (byte)PacketType.Unsubscribe | 0b0010;

        protected override int RemainingSize => 2 + _topics.Sum( s => s.MQTTSize() );

        protected override void WriteContent( Span<byte> span )
        {
            span = span.WriteUInt16( (ushort)PacketId );
            foreach( string topic in _topics )
            {
                span = span.WriteString( topic );
            }
        }
    }
}
