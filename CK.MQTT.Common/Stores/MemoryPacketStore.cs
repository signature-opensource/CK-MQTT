using CK.Core;
using CK.MQTT.Abstractions.Packets;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Stores
{
    public class MemoryPacketStore : PacketStore
    {
        class OutgoingStoredPacket : IOutgoingPacketWithId
        {
            readonly byte[] _buffer;

            public OutgoingStoredPacket( int packetId, QualityOfService qos, byte[] buffer )
            {
                PacketId = packetId;
                Qos = qos;
                _buffer = buffer;
            }
            public int PacketId { get; set; }

            public QualityOfService Qos { get; set; }

            public int GetSize() => _buffer.Length;

            public ValueTask WriteAsync( PipeWriter writer, CancellationToken cancellationToken )
                => writer.WriteAsync( _buffer ).AsNonGenericValueTask();
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
            var arr = new byte[packet.GetSize()];
            var pipe = PipeWriter.Create( new MemoryStream( arr ) );
            packet.WriteAsync( pipe, default );
            var newPacket = new OutgoingStoredPacket( packet.PacketId, packet.Qos, arr );
            _packets.Add( packet.PacketId, newPacket );
            return new ValueTask<IOutgoingPacketWithId>( newPacket );
        }
    }
}
