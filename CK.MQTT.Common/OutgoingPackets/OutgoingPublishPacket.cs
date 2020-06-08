using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.OutgoingPackets
{
    public class OutgoingPublishPacket : ComplexOutgoingPacket
    {
        readonly bool _dup;
        readonly bool _retain;
        readonly string _topic;
        readonly QualityOfService _qos;
        readonly ushort _packetId;
        readonly Func<int> _getPayloadSize;
        readonly Func<PipeWriter, CancellationToken, ValueTask> _payloadWriter;
        readonly bool _packetIdPresent;
        public OutgoingPublishPacket(
            bool dup,
            bool retain,
            string topic,
            QualityOfService qos,
            Func<int> getPayloadSize,
            Func<PipeWriter, CancellationToken, ValueTask> payloadWriter,
            ushort packetId = 0
            )
        {
            _dup = dup;
            _retain = retain;
            _topic = topic;
            _qos = qos;
            _packetIdPresent = _qos > QualityOfService.AtMostOnce;
            _packetId = packetId;
            _getPayloadSize = getPayloadSize;
            _payloadWriter = payloadWriter;
        }

        protected override PacketType PacketType => PacketType.Publish;

        const byte _dupFlag = 1 << 4;
        const byte _retainFlag = 1;

        protected override byte Header =>
            (byte)(
                (byte)PacketType |
                (byte)(_dup ? _dupFlag : 0) |
                (byte)_qos << 1 |
                (byte)(_retain ? _retainFlag : 0)
            );

        protected override int RemainingSize => HeaderSize + _getPayloadSize();

        protected override int HeaderSize => _topic.MQTTSize() + (_packetIdPresent ? 2 : 0);

        protected override void WriteHeaderContent( Span<byte> span )
        {
            span = span.WriteString( _topic );
            if( _packetIdPresent )
            {
                span.WriteUInt16( _packetId );
            }
        }

        protected override async ValueTask WriteRestOfThePacketAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            await _payloadWriter( pw, cancellationToken );
            await pw.FlushAsync( cancellationToken );
        }
    }
}
