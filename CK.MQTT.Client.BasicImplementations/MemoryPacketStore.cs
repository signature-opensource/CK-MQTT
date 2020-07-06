using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class MemoryPacketStore : PacketStore
    {
        class PipeWriterWrapper : PipeWriter
        {
            readonly PipeWriter _pipeWriter;
            bool _firstWrite = true;
            public PipeWriterWrapper(PipeWriter pipeWriter)
            {
                _pipeWriter = pipeWriter;
            }

            public override void Advance( int bytes ) => _pipeWriter.Advance( bytes );

            public override void CancelPendingFlush() => _pipeWriter.CancelPendingFlush();

            public override void Complete( Exception exception = null ) => _pipeWriter.Complete( exception );

            public override ValueTask<FlushResult> FlushAsync( CancellationToken cancellationToken = default ) => _pipeWriter.FlushAsync( cancellationToken );

            public override Memory<byte> GetMemory( int sizeHint = 0 ) => _pipeWriter.GetMemory( sizeHint );

            public override Span<byte> GetSpan( int sizeHint = 0 ) => _pipeWriter.GetSpan( sizeHint );

            public override Stream AsStream( bool leaveOpen = false ) => throw new NotImplementedException();

            public override ValueTask CompleteAsync( Exception exception = null ) => base.CompleteAsync( exception );

            protected override Task CopyFromAsync( Stream source, CancellationToken cancellationToken = default ) => throw new NotImplementedException();

            public override ValueTask<FlushResult> WriteAsync( ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default ) => base.WriteAsync( source, cancellationToken );
        }
        class OutgoingStoredPacket : IOutgoingPacketWithId
        {
            readonly byte[] _buffer;

            public OutgoingStoredPacket( int packetId, QualityOfService qos, byte[] buffer )
            {
                Qos = qos;
                _buffer = buffer;
                ((IOutgoingPacketWithId)(this)).PacketId = packetId;
            }

            int IOutgoingPacketWithId.PacketId { get; set; }

            public QualityOfService Qos { get; set; }

            public int Size => _buffer.Length;

            public async ValueTask<bool> WriteAsync( PipeWriter writer, CancellationToken cancellationToken )
            {
                await writer.WriteAsync( _buffer );
                return false;
            }
        }

        readonly Dictionary<int, OutgoingStoredPacket> _packets = new Dictionary<int, OutgoingStoredPacket>();

        public MemoryPacketStore( PacketTransformer packetTransformerOnRestore, PacketTransformer packetTransformerOnSave, int packetIdMaxValue )
            : base( packetTransformerOnRestore, packetTransformerOnSave, packetIdMaxValue )
        {
        }

        protected override ValueTask<QualityOfService> DoDiscardMessage( IMqttLogger m, int packetId )
        {
            QualityOfService qos = _packets[packetId].Qos;
            _packets.Remove( packetId );
            return new ValueTask<QualityOfService>( qos );
        }

        protected override ValueTask DoDiscardPacketIdAsync( IMqttLogger m, int packetId )
        {
            return new ValueTask(); //nothing to do, the packet id is not persisted.
        }

        protected override ValueTask<IOutgoingPacketWithId> DoStoreMessageAsync( IMqttLogger m, IOutgoingPacketWithId packet )
        {
            Debug.Assert( !_packets.ContainsKey( packet.PacketId ) );
            var arr = new byte[packet.Size];
            var pipe = PipeWriter.Create( new MemoryStream( arr ) );
            packet.WriteAsync( pipe, default );
            var newPacket = new OutgoingStoredPacket( packet.PacketId, packet.Qos, arr );
            _packets.Add( packet.PacketId, newPacket );
            return new ValueTask<IOutgoingPacketWithId>( newPacket );
        }

        protected override ValueTask DoReset()
        {
            _packets.Clear();
            return new ValueTask();
        }

        protected override ValueTask<IOutgoingPacketWithId> DoGetMessageByIdAsync( IMqttLogger m, int packetId )
            => new ValueTask<IOutgoingPacketWithId>( _packets[packetId] );
    }
}
