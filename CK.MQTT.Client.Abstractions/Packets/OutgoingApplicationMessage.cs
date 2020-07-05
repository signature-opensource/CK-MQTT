using System;

namespace CK.MQTT
{
    public abstract class OutgoingApplicationMessage : ComplexOutgoingPacket, IOutgoingPacketWithId
    {
        readonly bool _dup;
        readonly bool _retain;
        readonly bool _packetIdPresent;

        protected OutgoingApplicationMessage(
            bool dup,
            bool retain,
            string topic,
            QualityOfService qos
            )
        {
            _dup = dup;
            _retain = retain;
            Topic = topic;
            Qos = qos;
            _packetIdPresent = Qos > QualityOfService.AtMostOnce;
        }

        public string Topic { get; set; }

        public int PacketId { get; set; }

        public QualityOfService Qos { get; }

        protected sealed override int HeaderSize => Topic.MQTTSize() + (_packetIdPresent ? 2 : 0);

        const byte _dupFlag = 1 << 4;
        const byte _retainFlag = 1;

        protected sealed override byte Header =>
            (byte)(
                (byte)PacketType.Publish |
                (byte)(_dup ? _dupFlag : 0) |
                (byte)Qos << 1 |
                (byte)(_retain ? _retainFlag : 0)
            );


        protected sealed override void WriteHeaderContent( Span<byte> span )
        {
            span = span.WriteString( Topic );
            if( _packetIdPresent ) span.WriteUInt16( (ushort)PacketId );
        }
    }
}
