using CK.Core;
using CK.MQTT.Abstractions.Packets;
using CK.MQTT.Abstractions.Serialisation;
using CK.MQTT.Common;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Deserialization;
using CK.MQTT.Common.OutgoingPackets;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Stores;
using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Reflexes
{
    class PublishReflex : IReflexMiddleware
    {
        readonly IPacketIdStore _store;
        readonly Func<IActivityMonitor, IncomingApplicationMessage, Task> _deliverMessage;
        readonly OutgoingMessageHandler _output;

        public PublishReflex( IPacketIdStore store, Func<IActivityMonitor, IncomingApplicationMessage, Task> payloadProcessor, OutgoingMessageHandler output )
        {
            _store = store;
            _deliverMessage = payloadProcessor;
            _output = output;
        }
        const byte _dupFlag = 1 << 4;
        const byte _retainFlag = 1;

        public async ValueTask ProcessIncomingPacketAsync(
            IActivityMonitor m, IncomingMessageHandler sender,
            byte header, int packetLength, PipeReader reader, Func<ValueTask> next )
        {
            if( (PacketType)((header >> 4) << 4) != PacketType.Publish )
            {
                await next();
                return;
            }
            byte qosByte = (byte)((header << 5) >> 6);
            if( qosByte > 2 ) throw new ProtocolViolationException();
            QualityOfService qos = (QualityOfService)qosByte;
            bool dup = (header & _dupFlag) > 0;
            bool retain = (header & _retainFlag) > 0;
            string? topic;
            ushort packetId;
            SequencePosition position;
            while( true )
            {

                ReadResult read = await reader.ReadAsync();
                if( read.IsCanceled ) return;
                if( qos == QualityOfService.AtMostOnce )
                {
                    string theTopic = await reader.ReadMQTTString();
                    await _deliverMessage( m, new IncomingApplicationMessage( theTopic, reader, dup, retain, packetLength - theTopic.MQTTSize() ) );
                    return;
                }
                if( Publish.ParsePublishWithPacketId( read.Buffer, out topic, out packetId, out position ) ) break;
                reader.AdvanceTo( read.Buffer.Start, read.Buffer.End );
            }
            reader.AdvanceTo( position );
            var incomingMessage = new IncomingApplicationMessage( topic, reader, dup, retain, packetLength - 2 - topic.MQTTSize() );
            if( qos == QualityOfService.AtLeastOnce )
            {
                await _deliverMessage( m, incomingMessage );
                if( !_output.QueueReflexMessage( new OutgoingPuback( packetId ) ) )
                {
                    m.Warn( "Could not queue PubAck. Message Queue is full !!!" );
                }
            }
            if( qos == QualityOfService.ExactlyOnce )
            {
                await _store.StoreId( m, packetId );
                await _deliverMessage( m, incomingMessage );
                if( !_output.QueueReflexMessage( new OutgoingPubrec( packetId ) ) )
                {
                    m.Warn( "Could not queue PubAck. Message Queue is full !!!" );
                }
            }
            else
            {
                throw new ProtocolViolationException();
            }
        }
    }
}
