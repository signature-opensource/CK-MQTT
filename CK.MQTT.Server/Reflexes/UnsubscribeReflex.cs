using CK.MQTT.Client;
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
    class UnsubscribeReflex : IReflexMiddleware
    {
        private readonly ITopicManager _topicManager;
        readonly OutputPump _outputPump;
        readonly ProtocolLevel _protocolLevel;

        public UnsubscribeReflex( ITopicManager topicManager, OutputPump outputPump, ProtocolLevel protocolLevel )
        {
            _topicManager = topicManager;
            _outputPump = outputPump;
            _protocolLevel = protocolLevel;
        }

        public async ValueTask<(OperationStatus, bool)> ProcessIncomingPacketAsync( IMqtt3Sink sink, InputPump sender, byte header, uint packetLength, PipeReader pipeReader, CancellationToken cancellationToken )
        {
            if( (PacketType)((header >> 4) << 4) != PacketType.Unsubscribe )
            {
                return (OperationStatus.Done, false);
            }
            ReadResult read = await pipeReader.ReadAtLeastAsync( (int)packetLength, cancellationToken );
            if( read.Buffer.Length < packetLength ) return (OperationStatus.NeedMoreData, true); // Will happen when the reader is completed/cancelled.
            var buffer = read.Buffer.Slice( 0, packetLength );
            Parse( buffer, out ushort packetId, out List<string> filters );
            var arrFilter = filters.ToArray();
            await _topicManager.UnsubscribeAsync( arrFilter );
            _outputPump.TryQueueReflexMessage( OutgoingUnsubscribeAck.UnsubscribeAck( packetId ) );
            pipeReader.AdvanceTo( buffer.End );
            return (OperationStatus.Done, true);
        }

        void Parse( ReadOnlySequence<byte> buffer, out ushort packetId, out List<string> filters )
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
                filters.Add( topicFilter! );
            }
        }
    }
}
