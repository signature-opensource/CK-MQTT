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
        readonly IPacketStore _store;

        public SubackReflex( IPacketStore store )
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
                QualityOfService debugQos = await _store.OnMessageAck( m, packetId, qos );
                Debug.Assert( debugQos == QualityOfService.AtLeastOnce );
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
