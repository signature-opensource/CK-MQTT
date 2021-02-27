using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.OutgoingPackets
{
    public class FromMemoryOutgoingPacket : IOutgoingPacket
    {
        readonly ReadOnlyMemory<byte> _readOnlyMemory;

        public FromMemoryOutgoingPacket( ReadOnlyMemory<byte> readOnlyMemory )
            => _readOnlyMemory = readOnlyMemory;
        public int GetSize( ProtocolLevel protocolLevel ) => _readOnlyMemory.Length;


        public async ValueTask<IOutgoingPacket.WriteResult> WriteAsync( ProtocolLevel protocolLevel, PipeWriter writer, CancellationToken cancellationToken )
        {
            FlushResult res = await writer.WriteAsync( _readOnlyMemory, cancellationToken );
            if( res.IsCompleted ) return IOutgoingPacket.WriteResult.Written;
            return IOutgoingPacket.WriteResult.Cancelled;
        }
    }
}
