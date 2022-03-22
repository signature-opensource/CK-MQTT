using CK.MQTT.Client;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class PublishReflex : IReflexMiddleware
    {
        readonly Mqtt3ClientConfiguration _mqttConfiguration;
        readonly IRemotePacketStore _store;
        readonly Func<string, PipeReader, uint, QualityOfService, bool, CancellationToken, ValueTask> _messageHandler;
        readonly OutputPump _output;

        public PublishReflex( Mqtt3ClientConfiguration mqttConfiguration, IRemotePacketStore store, Func<string, PipeReader, uint, QualityOfService, bool, CancellationToken, ValueTask> messageHandler, OutputPump output )
        {
            _mqttConfiguration = mqttConfiguration;
            _store = store;
            _messageHandler = messageHandler;
            _output = output;
        }
        const byte _dupFlag = 1 << 4;
        const byte _retainFlag = 1;

        public async ValueTask<OperationStatus> ProcessIncomingPacketAsync( IMqtt3Sink? sink, InputPump sender, byte header, uint packetLength, PipeReader reader, Func<ValueTask<OperationStatus>> next, CancellationToken cancellationToken )
        {
            if( (PacketType)((header >> 4) << 4) != PacketType.Publish )
            {
                return await next();
            }
            QualityOfService qos = (QualityOfService)((header >> 1) & 3);
            bool dup = (header & _dupFlag) > 0;
            bool retain = (header & _retainFlag) > 0;
            if( (byte)qos > 2 ) throw new ProtocolViolationException( $"Parsed QoS byte is invalid({(byte)qos})." );
            string? topic;
            ushort packetId;
            while( true )
            {
                ReadResult read = await reader.ReadAsync( cancellationToken );
                if( read.IsCanceled ) return OperationStatus.NeedMoreData;
                if( qos == QualityOfService.AtMostOnce )
                {
                    bool ReadString( [NotNullWhen( true )] out string? theTopic )
                    {
                        SequenceReader<byte> seqReader = new( read.Buffer );
                        bool result = seqReader.TryReadMQTTString( out theTopic );
                        if( result )
                        {
                            reader.AdvanceTo( seqReader.Position );
                        }
                        else
                        {
                            reader.AdvanceTo( read.Buffer.Start, read.Buffer.End );
                        }
                        return result;
                    }
                    if( !ReadString( out string? theTopic ) )
                    {
                        continue;
                    }
                    await _messageHandler( theTopic, reader, packetLength - theTopic.MQTTSize(), qos, retain, cancellationToken );
                    return OperationStatus.Done;
                }
                if( Publish.ParsePublishWithPacketId( read.Buffer, out topic, out packetId, out SequencePosition position ) )
                {
                    reader.AdvanceTo( position );
                    break;
                }
                reader.AdvanceTo( read.Buffer.Start, read.Buffer.End );
            }
            if( qos == QualityOfService.AtLeastOnce )
            {
                await _messageHandler( topic, reader, packetLength - 2 - topic.MQTTSize(), qos, retain, cancellationToken );
                _output.QueueReflexMessage( LifecyclePacketV3.Puback( packetId ) );
                return OperationStatus.Done;
            }
            if( qos != QualityOfService.ExactlyOnce ) throw new ProtocolViolationException();
            await _store.StoreIdAsync( packetId );
            await _messageHandler( topic, reader, packetLength - 2 - topic.MQTTSize(), qos, retain, cancellationToken );
            _output.QueueReflexMessage( LifecyclePacketV3.Pubrec( packetId ) );
            return OperationStatus.Done;
        }
    }
}
