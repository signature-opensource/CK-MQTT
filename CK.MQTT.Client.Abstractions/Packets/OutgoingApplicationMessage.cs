using CK.MQTT.Common;
using CK.MQTT.Common.OutgoingPackets;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using System;

namespace CK.MQTT.Abstractions.Packets
{
    public abstract class OutgoingApplicationMessage : ComplexOutgoingPacket
    {
        readonly bool _dup;
        readonly bool _retain;
        readonly string _topic;
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
            _topic = topic;
            Qos = qos;
            _packetIdPresent = Qos > QualityOfService.AtMostOnce;
        }

        public ushort? PacketId { get; set; }

        public QualityOfService Qos { get; }

        protected sealed override int HeaderSize => _topic.MQTTSize() + (_packetIdPresent ? 2 : 0);

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
            span = span.WriteString( _topic );
            if( _packetIdPresent )
            {
                if( !PacketId.HasValue ) throw new InvalidOperationException( "PacketId not set !" );
                span.WriteUInt16( PacketId.Value );
            }
        }
    }
}
