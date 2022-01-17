using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.OutgoingPackets
{
    public class FromMemoryOutgoingPacket : IOutgoingPacket
    {
        readonly ReadOnlyMemory<byte> _readOnlyMemory;
        readonly int _packetId;

        public FromMemoryOutgoingPacket( ReadOnlyMemory<byte> readOnlyMemory, QualityOfService qos, int packetId, bool isRemoteOwnedPacketId )
        {
            Debug.Assert( readOnlyMemory.Length > 0 );
            _readOnlyMemory = readOnlyMemory;
            Qos = qos;
            _packetId = packetId;
            IsRemoteOwnedPacketId = isRemoteOwnedPacketId;
        }

        public QualityOfService Qos { get; }
        public bool IsRemoteOwnedPacketId { get; }
        public int PacketId { get => _packetId; set => throw new NotSupportedException(); }

        public int GetSize( ProtocolLevel protocolLevel ) => _readOnlyMemory.Length;

        public async ValueTask<IOutgoingPacket.WriteResult> WriteAsync( ProtocolLevel protocolLevel, PipeWriter writer, CancellationToken cancellationToken )
        {
            FlushResult res = await writer.WriteAsync( _readOnlyMemory, cancellationToken );
            if( res.IsCanceled ) return IOutgoingPacket.WriteResult.Cancelled;
            await writer.FlushAsync( cancellationToken );
            return IOutgoingPacket.WriteResult.Written;
        }
    }
}
