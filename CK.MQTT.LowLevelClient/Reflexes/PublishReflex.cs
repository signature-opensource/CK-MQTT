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
        readonly IRemotePacketStore _store;
        readonly Func<string, PipeReader, uint, QualityOfService, bool, CancellationToken, ValueTask> _messageHandler;
        readonly OutputPump _output;

        public PublishReflex( IRemotePacketStore store, Func<string, PipeReader, uint, QualityOfService, bool, CancellationToken, ValueTask> messageHandler, OutputPump output )
        {
            _store = store;
            _messageHandler = messageHandler;
            _output = output;
        }
        const byte _dupFlag = 1 << 4;
        const byte _retainFlag = 1;

        public async ValueTask<OperationStatus> ProcessIncomingPacketAsync( IMqtt3Sink sink, InputPump sender, byte header, uint packetLength, PipeReader reader, Func<ValueTask<OperationStatus>> next, CancellationToken cancellationToken )
        {
            if( (PacketType)((header >> 4) << 4) != PacketType.Publish )
            {
                return await next();
            }
            QualityOfService qos = (QualityOfService)((header >> 1) & 3);
            bool dup = (header & _dupFlag) > 0;
            if( dup )
            {
                sink?.OnPacketWithDupFlagReceived( PacketType.Publish );
            }
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
                if( ParsePublishWithPacketId( read.Buffer, out topic, out packetId, out SequencePosition position ) )
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

        /// <summary>
        /// Parse a publish packet with a packet id.
        /// Simply read the topic, packet id, and give their results by out parameters.
        /// </summary>
        /// <param name="buffer">The buffer to read the data from.</param>
        /// <param name="topic">The topic of the publish packet.</param>
        /// <param name="packetId">The packet id of the publish packet.</param>
        /// <param name="position">The position after the read data.</param>
        /// <returns>true if there was enough data, false if more data is required.</returns>
        static bool ParsePublishWithPacketId( ReadOnlySequence<byte> buffer, [NotNullWhen( true )] out string? topic, out ushort packetId, out SequencePosition position )
        {
            SequenceReader<byte> reader = new( buffer );
            if( !reader.TryReadMQTTString( out topic ) )
            {
                packetId = 0;
                position = reader.Position;
                return false;
            }
            if( !reader.TryReadBigEndian( out packetId ) )
            {
                position = reader.Position;
                return false;
            }
            position = reader.Position;
            return true;
        }
    }
}