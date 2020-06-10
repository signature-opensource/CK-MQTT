using CK.MQTT.Common.Packets;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.OutgoingPackets
{
    public abstract class ComplexOutgoingPacket : OutgoingPacket
    {
        /// <summary>
        /// The first byte of the packet, most of the time there is only the packet type written.
        /// This Property will be called when serializing the packet.
        /// </summary>
        protected abstract byte Header { get; }

        /// <summary>
        /// The total size of the packet.
        /// This Property will be called when serializing the packet.
        /// </summary>
        protected abstract int RemainingSize { get; }

        /// <summary>
        /// The minimum size of the <see cref="Span{byte}"/> that will be given when calling <see cref="WriteHeaderContent(Span{byte})"/>.
        /// This Property will be called when serializing the packet.
        /// </summary>
        protected abstract int HeaderSize { get; }

        protected void WriteHeader( PipeWriter pw )
        {
            int remainingSize = RemainingSize;
            int sizeOfSize = remainingSize.CompactByteCount();
            int bytesToWrite = HeaderSize + sizeOfSize + 1;
            Span<byte> span = pw.GetSpan( bytesToWrite );
            span[0] = Header;
            span = span[1..].WriteRemainingSize( remainingSize );
            WriteHeaderContent( span );
            pw.Advance( bytesToWrite );
        }

        protected abstract void WriteHeaderContent( Span<byte> span );

        protected abstract ValueTask WriteRestOfThePacketAsync( PipeWriter pw, CancellationToken cancellationToken );

        protected internal override ValueTask WriteAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            WriteHeader( pw );
            return WriteRestOfThePacketAsync( pw, cancellationToken );
        }
    }
}
