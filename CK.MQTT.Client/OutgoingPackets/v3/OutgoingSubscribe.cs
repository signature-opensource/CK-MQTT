using CK.Core;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CK.MQTT
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
                span = span.WriteMQTTString( _subscriptions[i].TopicFilter );
                span[0] = (byte)_subscriptions[i].MaximumQualityOfService;
                span = span[1..];
            }
        }
    }

    public static class SubscribeExtension
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
