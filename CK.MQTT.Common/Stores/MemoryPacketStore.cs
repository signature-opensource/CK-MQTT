using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common
{
    public class MemoryPacketStore : PacketStore
    {
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

            public bool Burned => false;

            public ValueTask WriteAsync( PipeWriter writer, CancellationToken cancellationToken )
                => writer.WriteAsync( _buffer ).AsNonGenericValueTask();
        }

        readonly Dictionary<int, OutgoingStoredPacket> _packets = new Dictionary<int, OutgoingStoredPacket>();

        public MemoryPacketStore( Func<IOutgoingPacketWithId, IOutgoingPacketWithId> transformer, int packetIdMaxValue )
            : base( transformer, packetIdMaxValue )
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
            return new ValueTask(); //nothing to do, the packet id is not persisted.
        }

        protected override ValueTask<IOutgoingPacketWithId> DoStoreMessageAsync( IActivityMonitor m, IOutgoingPacketWithId packet )
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

        protected override ValueTask<IOutgoingPacketWithId> DoGetMessageByIdAsync( IActivityMonitor m, int packetId )
            => new ValueTask<IOutgoingPacketWithId>( _packets[packetId] );
    }
}
