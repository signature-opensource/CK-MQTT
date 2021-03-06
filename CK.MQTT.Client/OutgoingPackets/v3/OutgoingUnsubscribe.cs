using CK.Core;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CK.MQTT
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

        protected override int GetRemainingSize( ProtocolLevel protocolLevel )
        {
            return 2 + _topics.Sum( s => s.MQTTSize() );
        }

        protected override void WriteContent( ProtocolLevel protocolLevel, Span<byte> span )
        {
            span = span.WriteBigEndianUInt16( (ushort)PacketId );
            foreach( string topic in _topics )
            {
                span = span.WriteMQTTString( topic );
            }
        }
    }

    public static class UnsubscribeExtensions
    {
        /// <summary>
        /// Unsubscribe the client from topics.
        /// </summary>
        /// <param name="m">The logger used to log the activities about the unsubscribe process.</param>
        /// <param name="topics">
        /// The list of topics to unsubscribe from.
        /// </param>
        /// <returns>
        /// The <see cref="ValueTask{TResult}"/> that complete when the Unsubscribe is guaranteed to be sent.
        /// The <see cref="Task"/> complete when the client receive the broker acknowledgement.
        /// Once the Task completes, no more application messages for those topics will arrive.</returns>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180885">MQTT Unsubscribe</a>
        /// for more details about the protocol unsubscription
        /// </remarks>
        public async static ValueTask<Task> UnsubscribeAsync( this IMqtt3Client client, IActivityMonitor m, params string[] topics )
         => await client.SendPacket<object>( m, new OutgoingUnsubscribe( topics ) );
    }
}
