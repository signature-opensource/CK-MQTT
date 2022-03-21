using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Packets
{
    public abstract class SimpleOutgoingPacket : IOutgoingPacket
    {
        public abstract ushort PacketId { get; set; }
        public abstract QualityOfService Qos { get; }
        public abstract bool IsRemoteOwnedPacketId { get; }

        /// <summary>
        /// Allow to write synchronously to the input buffer.
        /// </summary>
        /// <param name="protocolLevel"></param>
        /// <param name="buffer">The buffer to modify.</param>
        protected abstract void Write( ProtocolLevel protocolLevel, Span<byte> buffer );

        void Write( ProtocolLevel protocolLevel, PipeWriter pw )
        {
            int size = (int)GetSize( protocolLevel );
            Write( protocolLevel, pw.GetSpan( size ) );
            pw.Advance( size );
        }

        /// <inheritdoc/>
        public async ValueTask<WriteResult> WriteAsync( ProtocolLevel protocolLevel, PipeWriter pw, CancellationToken cancellationToken )
        {
            Write( protocolLevel, pw );
            await pw.FlushAsync( cancellationToken );
            return WriteResult.Written;
        }

        /// <inheritdoc/>
        public abstract uint GetSize( ProtocolLevel protocolLevel );
    }
}
