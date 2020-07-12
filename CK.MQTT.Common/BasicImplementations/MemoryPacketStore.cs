using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CK.MQTT.IOutgoingPacket;

namespace CK.MQTT
{
    /// <summary>
    /// Store <see cref="IOutgoingPacket"/> in memory.
    /// Does NOT save the data, if the process is killed, data is lost !
    /// </summary>
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

            public async ValueTask<WriteResult> WriteAsync( PipeWriter writer, CancellationToken cancellationToken )
            {
                await writer.WriteAsync( _buffer );
                return WriteResult.Written;
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
            => new ValueTask(); //nothing to do, the packet id is not persisted.

        /// <summary>
        /// Store the Message in memory. Packet is always reusable.
        /// </summary>
        /// <param name="m"></param>
        /// <param name="packet"></param>
        /// <returns></returns>
        protected async override ValueTask<IOutgoingPacketWithId> DoStoreMessageAsync( IMqttLogger m, IOutgoingPacketWithId packet )
        {
            if( _packets.ContainsKey( packet.PacketId ) ) throw new InvalidOperationException( $"Packet Id was badly choosen. Did you restored it's state correctly ?" );
            byte[] arr = new byte[packet.Size];//Some packet can be written only once. So we need to allocate memory for them.
            PipeWriter pipe = PipeWriter.Create( new MemoryStream( arr ) );//And write their content to this memory.
            if( await packet.WriteAsync( pipe, default ) != WriteResult.Written) throw new InvalidOperationException( "Didn't wrote packet correctly." );
            var newPacket = new OutgoingStoredPacket( packet.PacketId, packet.Qos, arr );
            _packets.Add( packet.PacketId, newPacket );
            return newPacket;
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
