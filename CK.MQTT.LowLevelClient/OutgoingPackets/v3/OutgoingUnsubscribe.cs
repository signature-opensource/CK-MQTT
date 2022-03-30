using System;
using System.Buffers.Binary;
using System.Linq;

namespace CK.MQTT.Packets
{
    class OutgoingUnsubscribe : VariableOutgointPacket
    {
        private readonly string[] _topics;

        public OutgoingUnsubscribe( string[] topics ) => _topics = topics;

        public override ushort PacketId { get; set; }
        public override bool IsRemoteOwnedPacketId => false;

        public override QualityOfService Qos => QualityOfService.AtLeastOnce;

        //The bit set is caused by MQTT-3.8.1-1: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc442180829
        protected override byte Header => (byte)PacketType.Unsubscribe | 0b0010;

        protected override uint GetRemainingSize( ProtocolLevel protocolLevel )
        {
            return 2 + (uint)_topics.Sum( s => s.MQTTSize() );
        }

        protected override void WriteContent( ProtocolLevel protocolLevel, Span<byte> span )
        {
            BinaryPrimitives.WriteUInt16BigEndian( span, (ushort)PacketId );
            span = span[2..];
            foreach( string topic in _topics )
            {
                span = span.WriteMQTTString( topic );
            }
        }
    }
}
