using CK.MQTT.Common;
using CK.MQTT.Common.OutgoingPackets;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using System;
using System.Linq;

namespace CK.MQTT.Client.OutgoingPackets
{
    class OutgoingSubscribe : VariableOutgointPacket
    {
        readonly ushort _packetId;
        readonly Subscription[] _subscriptions;

        public OutgoingSubscribe( ushort packetId, params Subscription[] subscriptions )
        {
            _packetId = packetId;
            _subscriptions = subscriptions;
        }

        protected override PacketType PacketType => PacketType.Subscribe;

        //The bit set is caused by MQTT-3.8.1-1: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349306
        protected override byte Header => (byte)PacketType.Subscribe | 0b0000_0010;

        protected override int RemainingSize => 2 + _subscriptions.Sum( s => s.TopicFilter.MQTTSize() + 1 );

        protected override void WriteContent( Span<byte> span )
        {
            span = span.WriteUInt16( _packetId );
            for( int i = 0; i < _subscriptions.Length; i++ )
            {
                span = span.WriteString( _subscriptions[i].TopicFilter );
                span[0] = (byte)_subscriptions[i].MaximumQualityOfService;
                span = span[1..];
            }
        }
    }
}
