using CK.Core;
using CK.MQTT.Client.Deserialization;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Processes
{
    public class PublishReflex : IReflexMiddleware
    {
        public delegate ValueTask PayloadProcessor( PipeReader pipeReader, string topic, int payloadSize );

        readonly PayloadProcessor _payloadProcessor;

        public PublishReflex( PayloadProcessor payloadProcessor )
        {
            _payloadProcessor = payloadProcessor;
        }
        const byte _dupFlag = 1 << 4;
        const byte _retainFlag = 1;

        public async ValueTask ProcessIncomingPacket(
            IActivityMonitor m,
            byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            byte qosByte = (byte)((header << 5) >> 6);
            if( qosByte > 2 ) throw new ProtocolViolationException();
            QualityOfService qos = (QualityOfService)qosByte;
            if( (PacketType)((header >> 4) << 4) != PacketType.Publish )
            {
                await next();
                return;
            }
            Parse:
            ReadResult read = await pipeReader.ReadAsync();
            if( read.IsCanceled ) return;
            if( qos == QualityOfService.AtMostOnce )
            {
                string theTopic = await pipeReader.ReadMQTTString();
                await _payloadProcessor( pipeReader, theTopic, packetLength - theTopic.MQTTSize() );
                return;
            }
            OperationStatus result = Publish.ParsePublishWithPacketId( read.Buffer, out string? topic, out ushort packetId );
            if( result == OperationStatus.NeedMoreData )
            {
                pipeReader.AdvanceTo( read.Buffer.Start, read.Buffer.End );
                goto Parse;
            }
            if( qos == QualityOfService.AtLeastOnce )
            {
                await _payloadProcessor( pipeReader, topic, packetLength - 2 - topic.MQTTSize() );
            }
        }
    }
}
