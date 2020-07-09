using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class PublishReflex : IReflexMiddleware
    {
        readonly IPacketIdStore _store;
        readonly Func<IMqttLogger, IncomingMessage, Task> _deliverMessage;
        readonly OutgoingMessageHandler _output;

        public PublishReflex( IPacketIdStore store, Func<IMqttLogger, IncomingMessage, Task> payloadProcessor, OutgoingMessageHandler output )
        {
            _store = store;
            _deliverMessage = payloadProcessor;
            _output = output;
        }
        const byte _dupFlag = 1 << 4;
        const byte _retainFlag = 1;

        public async ValueTask ProcessIncomingPacketAsync(
            IMqttLogger m, IncomingMessageHandler sender,
            byte header, int packetLength, PipeReader reader, Func<ValueTask> next )
        {
            if( (PacketType)((header >> 4) << 4) != PacketType.Publish )
            {
                await next();
                return;
            }
            m.Trace( $"Handling incoming packet as {PacketType.Publish}." );
            byte qosByte = (byte)((header >> 1) & 3);
            if( qosByte > 2 ) throw new ProtocolViolationException();
            QualityOfService qos = (QualityOfService)qosByte;
            bool dup = (header & _dupFlag) > 0;
            bool retain = (header & _retainFlag) > 0;
            string? topic;
            ushort packetId;
            while( true )
            {

                ReadResult read = await reader.ReadAsync();
                if( read.IsCanceled ) return;
                if( qos == QualityOfService.AtMostOnce )
                {
                    string theTopic = await reader.ReadMQTTString();
                    await _deliverMessage( m, new IncomingMessage( theTopic, reader, dup, retain, packetLength - theTopic.MQTTSize() ) );
                    return;
                }
                if( Publish.ParsePublishWithPacketId( read.Buffer, out topic, out packetId, out SequencePosition position ) )
                {
                    reader.AdvanceTo( position );
                    break;
                }
                reader.AdvanceTo( read.Buffer.Start, read.Buffer.End );
            }
            var incomingMessage = new IncomingMessage( topic, reader, dup, retain, packetLength - 2 - topic.MQTTSize() );
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
