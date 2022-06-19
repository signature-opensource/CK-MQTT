using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Packets
{
    public class FromMemoryOutgoingPacket : IOutgoingPacket
    {
        readonly ReadOnlyMemory<byte> _readOnlyMemory;
        readonly ushort _packetId;

        public FromMemoryOutgoingPacket( PacketType packetType, ReadOnlyMemory<byte> readOnlyMemory, QualityOfService qos, ushort packetId, bool isRemoteOwnedPacketId )
        {
            Debug.Assert( readOnlyMemory.Length > 0 );
            _readOnlyMemory = readOnlyMemory;
            Qos = qos;
            _packetId = packetId;
            IsRemoteOwnedPacketId = isRemoteOwnedPacketId;
            Type = packetType;
        }

        public PacketType Type { get; }
        public QualityOfService Qos { get; }
        public ushort PacketId { get => _packetId; set => throw new NotSupportedException(); }
        public bool IsRemoteOwnedPacketId { get; }

        public uint GetSize( ProtocolLevel protocolLevel ) => (uint)_readOnlyMemory.Length;

        public ValueTask WriteAsync( ProtocolLevel protocolLevel, PipeWriter writer, CancellationToken cancellationToken )
        {
            _readOnlyMemory.Span.CopyTo( writer.GetSpan( _readOnlyMemory.Length ) );
            writer.Advance( _readOnlyMemory.Length );
            //FlushResult res = await writer.WriteAsync( _readOnlyMemory, cancellationToken );
            //if( res.IsCanceled ) return WriteResult.Cancelled;
            return new ValueTask();
        }
    }
}
