using CK.MQTT.Common;
using CK.MQTT.Common.OutgoingPackets;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.MQTT.Abstractions.Packets
{
    public abstract class OutgoingApplicationMessage : ComplexOutgoingPacket
    {
        readonly bool _dup;
        readonly bool _retain;
        readonly string _topic;
        readonly QualityOfService _qos;
        readonly ushort _packetId;
        readonly bool _packetIdPresent;
        protected sealed override PacketType PacketType => PacketType.Publish;

        protected OutgoingApplicationMessage(
            bool dup,
            bool retain,
            string topic,
            QualityOfService qos,
            ushort packetId = 0
            )
        {
            _dup = dup;
            _retain = retain;
            _topic = topic;
            _qos = qos;
            _packetIdPresent = _qos > QualityOfService.AtMostOnce;
            _packetId = packetId;
        }

        protected sealed override int HeaderSize => _topic.MQTTSize() + (_packetIdPresent ? 2 : 0);

        const byte _dupFlag = 1 << 4;
        const byte _retainFlag = 1;

        protected sealed override byte Header =>
            (byte)(
                (byte)PacketType |
                (byte)(_dup ? _dupFlag : 0) |
                (byte)_qos << 1 |
                (byte)(_retain ? _retainFlag : 0)
            );

        protected sealed override void WriteHeaderContent( Span<byte> span )
        {
            span = span.WriteString( _topic );
            if( _packetIdPresent )
            {
                span.WriteUInt16( _packetId );
            }
        }
    }
}
