using System;
using System.Linq;

namespace CK.MQTT.Client
{
    class OutgoingSubscribe : VariableOutgointPacket, IOutgoingPacketWithId
    {
        readonly Subscription[] _subscriptions;

        public OutgoingSubscribe( Subscription[] subscriptions )
        {
            _subscriptions = subscriptions;
        }

        public int PacketId { get; set; }
        public QualityOfService Qos => QualityOfService.AtLeastOnce;

        //The bit set is caused by MQTT-3.8.1-1: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349306
        protected override byte Header => (byte)PacketType.Subscribe | 0b0010;
        protected override int RemainingSize => 2 + _subscriptions.Sum( s => s.TopicFilter.MQTTSize() + 1 );

        protected override void WriteContent( Span<byte> span )
        {
            span = span.WriteUInt16( (ushort)PacketId );
            for( int i = 0; i < _subscriptions.Length; i++ )
            {
                span = span.WriteString( _subscriptions[i].TopicFilter );
                span[0] = (byte)_subscriptions[i].MaximumQualityOfService;
                span = span[1..];
            }
        }
    }
}
