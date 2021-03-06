using CK.MQTT.Stores;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class SubackReflex : IReflexMiddleware
    {
        readonly IMqttIdStore _store;

        public SubackReflex( IMqttIdStore store )
        {
            _store = store;
        }
        public async ValueTask ProcessIncomingPacketAsync( IInputLogger? m, InputPump sender, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next, CancellationToken cancellationToken )
        {
            if( PacketType.SubscribeAck != (PacketType)header )
            {
                await next();
                return;
            }
            using( m?.ProcessPacket( PacketType.SubscribeAck ) )
            {
                ReadResult? read = await pipeReader.ReadAsync( m, packetLength );
                if( !read.HasValue ) return;
                Parse( read.Value.Buffer, packetLength, out ushort packetId, out QualityOfService[]? qos, out SequencePosition position );
                pipeReader.AdvanceTo( position );
                await _store.OnQos1AckAsync( m, packetId, qos );
            }
        }

        static void Parse( ReadOnlySequence<byte> buffer,
            int payloadLength,
            out ushort packetId,
            [NotNullWhen( true )] out QualityOfService[]? qos,
            out SequencePosition position )
        {
            SequenceReader<byte> reader = new SequenceReader<byte>( buffer );
            if( !reader.TryReadBigEndian( out packetId ) ) throw new InvalidOperationException();
            buffer = buffer.Slice( 2, payloadLength - 2 );
            qos = (QualityOfService[])(object)buffer.ToArray();
            position = buffer.End;
        }
    }
}
