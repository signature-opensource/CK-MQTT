using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class MemoryPacketStore : PacketStore
    {
        class OutgoingStoredPacket : IOutgoingPacketWithId
        {
            readonly ReadOnlyMemory<byte> _buffer;

            public OutgoingStoredPacket( int packetId, QualityOfService qos, ReadOnlyMemory<byte> buffer )
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

        public MemoryPacketStore( IStoreTransformer storeTransformer, int packetIdMaxValue )
            : base( storeTransformer, packetIdMaxValue )
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
            byte[] arr = new byte[packet.Size];
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

        protected override ValueTask<IAsyncEnumerable<IOutgoingPacketWithId>> DoGetAllMessagesAsync( IMqttLogger m )
            => new ValueTask<IAsyncEnumerable<IOutgoingPacketWithId>>( _packets.Select( s => (IOutgoingPacketWithId)s.Value ).ToAsyncEnumerable() );
    }
}
