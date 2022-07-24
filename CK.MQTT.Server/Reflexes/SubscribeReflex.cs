using CK.MQTT.Client;
using CK.MQTT.Packets;
using CK.MQTT.Pumps;
using CK.MQTT.Server.OutgoingPackets;
using CK.MQTT.Stores;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server.Reflexes
{
    class SubscribeReflex : IReflexMiddleware
    {
        readonly IMQTTServerSink _sink;
        readonly ProtocolLevel _protocolLevel;
        readonly OutputPump _output;

        public SubscribeReflex( IMQTTServerSink sink, ProtocolLevel protocolLevel, OutputPump output )
        {
            _sink = sink;
            _protocolLevel = protocolLevel;
            _output = output;
        }

        public async ValueTask<(OperationStatus, bool)> ProcessIncomingPacketAsync( IMQTT3Sink sink, InputPump sender, byte header, uint packetLength, PipeReader pipeReader, CancellationToken cancellationToken )
        {
            if( (PacketType)((header >> 4) << 4) != PacketType.Subscribe )
            {
                return (OperationStatus.Done, false);
            }
            ReadResult read = await pipeReader.ReadAtLeastAsync( (int)packetLength, cancellationToken );
            if( read.Buffer.Length < packetLength ) return (OperationStatus.NeedMoreData, true); // Will happen when the reader is completed/cancelled.
            var buffer = read.Buffer.Slice( 0, packetLength );
            Parse( buffer, out ushort packetId, out List<Subscription> filters );
            var subscribeReturnCodes = await _sink.OnSubscribeAsync( filters.ToArray() );
            _output.TryQueueReflexMessage( new OutgoingSubscribeAck( packetId, subscribeReturnCodes ) );
            pipeReader.AdvanceTo( buffer.End );
            return (OperationStatus.Done, true);
        }

        void Parse( ReadOnlySequence<byte> buffer, out ushort packetId, out List<Subscription> filters )
        {
            filters = new();
            var reader = new SequenceReader<byte>( buffer );
            if( !reader.TryReadBigEndian( out packetId ) ) throw new InvalidOperationException();
            if( _protocolLevel == ProtocolLevel.MQTT5 )
            {
                reader.TryReadVariableByteInteger( out uint propertyLength );
                reader.Advance( propertyLength );
                //TODO: parse properties
            }
            while( !reader.End )
            {
                reader.TryReadMQTTString( out string? topicFilter );
                reader.TryRead( out byte qos );
                qos = (byte)((qos << 6) >> 6);
                filters.Add( new Subscription( topicFilter!, (QualityOfService)qos ) );
            }
        }
    }
}
