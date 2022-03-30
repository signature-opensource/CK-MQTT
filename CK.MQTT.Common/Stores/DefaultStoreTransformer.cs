using CK.MQTT.Packets;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class DefaultStoreTransformer : IStoreTransformer
    {
        class PipeWriterWrapper : PipeWriter
        {
            private readonly PipeWriter _pw;
            bool _firstWrite = true;
            public PipeWriterWrapper( PipeWriter pw ) => _pw = pw;

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
        class PacketWrapper : IOutgoingPacket
        {
            readonly IOutgoingPacket _packet;

            public PacketWrapper( IOutgoingPacket packet ) => _packet = packet;

            public ushort PacketId { get => _packet.PacketId; set => _packet.PacketId = value; }
            public bool IsRemoteOwnedPacketId => _packet.IsRemoteOwnedPacketId;

            public QualityOfService Qos => _packet.Qos;

            public uint GetSize( ProtocolLevel protocolLevel ) => _packet.GetSize( protocolLevel );

            public ValueTask<WriteResult> WriteAsync( ProtocolLevel protocolLevel, PipeWriter writer, CancellationToken cancellationToken )
                => _packet.WriteAsync( protocolLevel, new PipeWriterWrapper( writer ), cancellationToken );

            public override string ToString() => $"DefaultStoreTransformer({_packet})"; //Helps debugging.
        }

        static byte TransformerLogic( byte header )
        {
            if( PacketType.Publish != (PacketType)((header >> 4) << 4) ) return header;
            return header |= 0b100;
        }

        protected static IOutgoingPacket SetDup( IOutgoingPacket arg ) => new PacketWrapper( arg );

        public Func<IOutgoingPacket, IOutgoingPacket> PacketTransformerOnRestore => SetDup;

        public Func<IOutgoingPacket, IOutgoingPacket> PacketTransformerOnSave => ( arg ) => arg;

        public static DefaultStoreTransformer Default => new();
    }
}
