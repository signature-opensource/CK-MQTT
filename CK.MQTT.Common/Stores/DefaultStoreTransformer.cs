using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static CK.MQTT.IOutgoingPacket;
using static CK.MQTT.PacketStore;

namespace CK.MQTT
{
    public class DefaultStoreTransformer : IStoreTransformer
    {
        class PipeWriterWrapper : PipeWriter
        {
            private readonly PipeWriter _pw;
            bool _firstWrite = true;
            public PipeWriterWrapper( PipeWriter pw )
            {
                _pw = pw;
            }

            public override void Advance( int bytes )
            {
                if( bytes > 0 && _firstWrite )
                {
                    _firstWrite = false;
                    Span<byte> span = _pw.GetSpan( 1 );
                    span[0] = TransformerLogic( span[0] );
                }
                _pw.Advance( bytes );
            }

            public override void CancelPendingFlush() => _pw.CancelPendingFlush();

            public override void Complete( Exception? exception = null ) => _pw.Complete( exception );

            public override ValueTask<FlushResult> FlushAsync( CancellationToken cancellationToken = default ) => _pw.FlushAsync( cancellationToken );

            public override Memory<byte> GetMemory( int sizeHint = 0 ) => _pw.GetMemory( sizeHint );

            public override Span<byte> GetSpan( int sizeHint = 0 ) => _pw.GetSpan( sizeHint );
        }
        class PacketWrapper : IOutgoingPacketWithId
        {
            readonly IOutgoingPacketWithId _packet;

            public PacketWrapper( IOutgoingPacketWithId packet )
            {
                _packet = packet;
            }

            public int PacketId { get => _packet.PacketId; set => _packet.PacketId = value; }

            public QualityOfService Qos => _packet.Qos;

            public int Size => _packet.Size;

            public ValueTask<WriteResult> WriteAsync( PipeWriter writer, CancellationToken cancellationToken ) => _packet.WriteAsync( new PipeWriterWrapper( writer ), cancellationToken );
        }

        static byte TransformerLogic( byte header )
        {
            if( PacketType.Publish != (PacketType)((header >> 4) << 4) ) return header;
            return header |= 0b100;
        }

        protected static IOutgoingPacketWithId SetDup( IOutgoingPacketWithId arg ) => new PacketWrapper( arg );

        public Func<IOutgoingPacketWithId, IOutgoingPacketWithId> PacketTransformerOnRestore => ( IOutgoingPacketWithId arg ) => arg;

        public Func<IOutgoingPacketWithId, IOutgoingPacketWithId> PacketTransformerOnSave => SetDup;

        public static DefaultStoreTransformer Default => new DefaultStoreTransformer();
    }
}
