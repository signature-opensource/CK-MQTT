using CK.MQTT.Client;
using CK.MQTT.P2P;
using CK.MQTT.Packets;
using CK.MQTT.Pumps;
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
        readonly ITopicManager _topicManager;
        readonly ProtocolLevel _protocolLevel;
        readonly OutputPump _output;

        public SubscribeReflex( ITopicManager topicManager, ProtocolLevel protocolLevel, OutputPump output )
        {
            _topicManager = topicManager;
            _protocolLevel = protocolLevel;
            _output = output;
        }

        public async ValueTask<OperationStatus> ProcessIncomingPacketAsync( IMqtt3Sink sink, InputPump sender, byte header, uint packetLength, PipeReader pipeReader, Func<ValueTask<OperationStatus>> next, CancellationToken cancellationToken )
        {
            if( (PacketType)((header >> 4) << 4) != PacketType.Subscribe )
            {
                return await next();
            }
            ReadResult read = await pipeReader.ReadAtLeastAsync( (int)packetLength, cancellationToken );
            if( read.Buffer.Length < packetLength ) return OperationStatus.NeedMoreData; // Will happen when the reader is completed/cancelled.
            var buffer = read.Buffer.Slice( 0, packetLength );
            Parse( buffer, out ushort packetId, out List<Subscription> filters );
            var subscribeReturnCodes = await _topicManager.SubscribeAsync( filters.ToArray() );
            _output.QueueReflexMessage( new OutgoingSubscribeAck( packetId, subscribeReturnCodes ) );
            pipeReader.AdvanceTo( buffer.End );
            return OperationStatus.Done;
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
