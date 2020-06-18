using CK.Core;
using CK.MQTT.Abstractions.Packets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Stores
{
    public class MemoryPacketStore : PacketStore
    {
        class OutgoingStoredPacket : IOutgoingPacketWithId
        {
            readonly ReadOnlyMemory<byte> _memory;

            public OutgoingStoredPacket( int packetId, QualityOfService qos, ReadOnlyMemory<byte> stream )
            {
                PacketId = packetId;
                Qos = qos;
                _memory = stream;
            }
            public int PacketId { get; set; }

            public QualityOfService Qos { get; set; }

            public int GetSize() => _memory.Length;

            public ValueTask WriteAsync( PipeWriter writer, CancellationToken cancellationToken )
                => writer.WriteAsync( _memory ).AsNonGenericValueTask();
        }

        readonly Dictionary<int, OutgoingStoredPacket> _packets = new Dictionary<int, OutgoingStoredPacket>();

        public MemoryPacketStore( int packetIdMaxValue ) : base( packetIdMaxValue )
        {
        }

        protected override ValueTask<QualityOfService> DoDiscardMessage( IActivityMonitor m, int packetId )
        {
            QualityOfService qos = _packets[packetId].Qos;
            _packets.Remove( packetId );
            return new ValueTask<QualityOfService>( qos );
        }

        protected override ValueTask DoDiscardPacketIdAsync( IActivityMonitor m, int packetId )
        {
            //nothing to do, the packet id is not persisted.
            return new ValueTask();
        }

        protected override ValueTask<IOutgoingPacketWithId> DoStoreMessageAsync( IActivityMonitor m, IOutgoingPacketWithId packet )
        {
            Debug.Assert( !_packets.ContainsKey( packet.PacketId ) );
            Memory<byte> memory = new Memory<byte>( new byte[packet.GetSize()] );
            _packets.Add( packet.PacketId, new OutgoingStoredPacket( packet.PacketId, packet.Qos, memory ) );
            return new ValueTask<IOutgoingPacketWithId>( new OutgoingStoredPacket( packet.PacketId, packet.Qos, memory ) );
        }
    }
}
