using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class OutgoingSubscribe : VariableOutgointPacket, IOutgoingPacketWithId
    {
        readonly Subscription[] _subscriptions;
        readonly int _subscriptionIdentifier;
        readonly IReadOnlyList<UserProperty>? _userProperties;
        readonly int _propertiesLength;
        public OutgoingSubscribe( Subscription[] subscriptions, int subscriptionIdentifier = 0, IReadOnlyList<UserProperty>? userProperties = null )
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
                _propertiesLength += userProperties.Sum( s => s.Size );
            }
        }

        public int PacketId { get; set; }
        public QualityOfService Qos => QualityOfService.AtLeastOnce;

        //The bit set is caused by MQTT-3.8.1-1: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349306
        protected override byte Header => (byte)PacketType.Subscribe | 0b0010;

        protected override int GetRemainingSize( ProtocolLevel protocolLevel )
            => 2 //PacketId
            + _subscriptions.Sum( s => s.TopicFilter.MQTTSize() + 1 ) //topics
            + (protocolLevel > ProtocolLevel.MQTT3 ? _propertiesLength.CompactByteCount() : 0)
            + _propertiesLength;


        protected override void WriteContent( ProtocolLevel protocolLevel, Span<byte> span )
        {
            span = span.WriteBigEndianUInt16( (ushort)PacketId );
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

    public static class SubscribeExtensions
    {
        /// <summary>
        /// Susbscribe the <see cref="IMqtt3Client"/> to a <a href="docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Ref374621403">Topic</a>.
        /// </summary>
        /// <param name="m">The logger used to log the activities about the subscription process.</param>
        /// <param name="subscriptions">The subscriptions to send to the broker.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that complete when the subscribe is guaranteed to be sent.
        /// The <see cref="Task{T}"/> complete when the client received the <a href="docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc384800441">Subscribe acknowledgement</a>.
        /// It's Task result contain a <see cref="SubscribeReturnCode"/> per subcription, with the same order than the array given in parameters.
        /// </returns>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180876">MQTT Subscribe</a>
        /// for more details about the protocol subscription.
        /// </remarks>
        public static ValueTask<Task<SubscribeReturnCode[]?>> SubscribeAsync( this IMqtt3Client client, IActivityMonitor m, params Subscription[] subscriptions )
            => client.SendPacket<SubscribeReturnCode[]>( m, new OutgoingSubscribe( subscriptions ) );
    }
}
