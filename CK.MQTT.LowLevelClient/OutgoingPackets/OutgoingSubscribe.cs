using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.MQTT.Packets
{
    class OutgoingSubscribe : VariableOutgointPacket, IOutgoingPacket
    {
        readonly Subscription[] _subscriptions;
        readonly uint _subscriptionIdentifier;
        readonly IReadOnlyList<UserProperty>? _userProperties;
        readonly uint _propertiesLength;
        public OutgoingSubscribe( Subscription[] subscriptions, uint subscriptionIdentifier = 0, IReadOnlyList<UserProperty>? userProperties = null )
        {
            _subscriptions = subscriptions;
            _subscriptionIdentifier = subscriptionIdentifier;
            _userProperties = userProperties;
            _propertiesLength = 0;
            if( subscriptionIdentifier > 0 )
            {
                _propertiesLength += 1 + subscriptionIdentifier.CompactByteCount();
            }
            if( _userProperties != null )
            {
                _propertiesLength += (uint)_userProperties.Sum( s => s.Size );
            }
        }

        public override ushort PacketId { get; set; }
        public override QualityOfService Qos => QualityOfService.AtLeastOnce;
        public override PacketType Type => PacketType.Subscribe;

        public override bool IsRemoteOwnedPacketId => false;

        //The bit set is caused by MQTT-3.8.1-1: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349306
        protected override byte Header => (byte)PacketType.Subscribe | 0b0010;

        protected override uint GetRemainingSize( ProtocolLevel protocolLevel )
            => 2 //PacketId
            + ((uint)_subscriptions.Select( s => s.TopicFilter.MQTTSize() + 1 ).Sum( s => (long)s )) //topics
            + (protocolLevel > ProtocolLevel.MQTT3 ? _propertiesLength.CompactByteCount() : 0)
            + _propertiesLength;


        protected override void WriteContent( ProtocolLevel protocolLevel, Span<byte> span )
        {
            BinaryPrimitives.WriteUInt16BigEndian( span, (ushort)PacketId );
            span = span[2..];
            if( protocolLevel > ProtocolLevel.MQTT3 )
            {
                span = span.WriteVariableByteInteger( _propertiesLength );
                if( _subscriptionIdentifier != 0 )
                {
                    span[0] = (byte)PropertyIdentifier.SubscriptionIdentifier;
                    span = span[1..].WriteVariableByteInteger( _subscriptionIdentifier );
                }
                if( _userProperties != null && _userProperties.Count > 0 )
                {
                    foreach( UserProperty prop in _userProperties )
                    {
                        span = prop.Write( span );
                    }
                }
            }
            else
            {
                Debug.Assert( _propertiesLength == 0 );
            }
            for( int i = 0; i < _subscriptions.Length; i++ )
            {
                span = span.WriteMQTTString( _subscriptions[i].TopicFilter );
                span[0] = (byte)_subscriptions[i].MaximumQualityOfService;
                span = span[1..];
            }
        }
    }
}
