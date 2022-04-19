using CK.MQTT.Client;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class SubackReflex : IReflexMiddleware
    {
        readonly ILocalPacketStore _store;

        public SubackReflex( ILocalPacketStore store )
        {
            _store = store;
        }
        public async ValueTask<OperationStatus> ProcessIncomingPacketAsync( IMqtt3Sink sink, InputPump sender, byte header, uint packetLength, PipeReader pipeReader, Func<ValueTask<OperationStatus>> next, CancellationToken cancellationToken )
        {
            if( PacketType.SubscribeAck != (PacketType)header )
            {
                return await next();
            }
            ReadResult read = await pipeReader.ReadAtLeastAsync( (int)packetLength, cancellationToken );
            if( read.Buffer.Length < packetLength ) return OperationStatus.NeedMoreData; // Will happen when the reader is completed/cancelled.
            Parse( read.Buffer, packetLength, out ushort packetId, out QualityOfService[]? qos, out SequencePosition position );
            pipeReader.AdvanceTo( position );
            await _store.OnQos1AckAsync( sink, packetId, qos );
            return OperationStatus.Done;
        }

        static void Parse( ReadOnlySequence<byte> buffer,
            uint payloadLength,
            out ushort packetId,
            [NotNullWhen( true )] out QualityOfService[]? qos,
            out SequencePosition position )
        {
            SequenceReader<byte> reader = new( buffer );
            if( !reader.TryReadBigEndian( out packetId ) ) throw new InvalidOperationException();
            buffer = buffer.Slice( 2, payloadLength - 2 );
            qos = (QualityOfService[])(object)buffer.ToArray();
            position = buffer.End;
        }
    }
}
