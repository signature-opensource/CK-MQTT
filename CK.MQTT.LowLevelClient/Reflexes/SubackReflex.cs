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
        readonly MessageExchanger _exchanger;

        public SubackReflex( MessageExchanger exchanger )
        {
            _exchanger = exchanger;
        }
        public async ValueTask<(OperationStatus, bool)> ProcessIncomingPacketAsync( IMqtt3Sink sink, InputPump sender, byte header, uint packetLength, PipeReader pipeReader, CancellationToken cancellationToken )
        {
            if( PacketType.SubscribeAck != (PacketType)header )
            {
                return (OperationStatus.Done, false);
            }
            ReadResult read = await pipeReader.ReadAtLeastAsync( (int)packetLength, cancellationToken );
            if( read.Buffer.Length < packetLength ) return (OperationStatus.NeedMoreData, true); // Will happen when the reader is completed/cancelled.
            Parse( read.Buffer, packetLength, out ushort packetId, out QualityOfService[]? qos, out SequencePosition position );
            pipeReader.AdvanceTo( position );
            bool detectedDrop = await _exchanger.LocalPacketStore.OnQos1AckAsync( sink, packetId, qos );
            if( detectedDrop )
            {
                _exchanger.Pumps!.Left.UnblockWriteLoop();
            }
            return (OperationStatus.Done, true);
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
