using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Packets;

/// <summary>
/// An <see cref="IOutgoingPacket"/> that have a variable header writable synchronously, and a variable payload writable asynchronously. <br/>
/// The <see cref="WriteHeader(ProtocolLevel, PipeWriter)"/> method is to serialize in memory data, like the topic string. 
/// The <see cref="WritePayloadAsync(ProtocolLevel, PipeWriter, CancellationToken)"/> is to write data that may not be in memory actually (like the payload).
/// </summary>
public abstract class ComplexOutgoingPacket : IOutgoingPacket
{
    public abstract PacketType Type { get; }
    /// <summary>
    /// The first byte of the packet. This contain the <see cref="PacketType"/> and possibly other data.
    /// </summary>
    protected abstract byte Header { get; }
    public abstract ushort PacketId { get; set; }
    public abstract QualityOfService Qos { get; }
    public abstract bool IsRemoteOwnedPacketId { get; }

    /// <inheritdoc/>
    public uint GetSize( ProtocolLevel protocolLevel )
        => GetRemainingSize( protocolLevel ) + 1 + GetRemainingSize( protocolLevel ).CompactByteCount();

    /// <summary>
    /// The <see cref="GetSize()"/>, minus the header, and the bytes required to write the <see cref="GetRemainingSize()"/> itself.
    /// </summary>
    private uint GetRemainingSize( ProtocolLevel protocolLevel )
        => GetHeaderSize( protocolLevel ) + GetPayloadSize( protocolLevel );

    /// <summary>
    /// The size of the Payload to write asynchronously.
    /// This is the amount of bytes that MUST be written when <see cref="WritePayloadAsync(PipeWriter, CancellationToken)"/> will be called.
    /// </summary>
    protected abstract uint GetPayloadSize( ProtocolLevel protocolLevel );

    /// <summary>
    /// The size of the Header.
    /// This is also the size of the <see cref="Span{T}"/> given when <see cref="WriteHeaderContent(Span{byte})"/> will be called.
    /// </summary>
    protected abstract uint GetHeaderSize( ProtocolLevel protocolLevel );

    /// <summary>
    /// Write the Header, remaining size, and call <see cref="WriteHeaderContent(Span{byte})"/>.
    /// </summary>
    /// <param name="pw">The <see cref="PipeWriter"/> to write to.</param>
    protected void WriteHeader( ProtocolLevel protocolLevel, PipeWriter pw )
    {
        uint headerSize = GetHeaderSize( protocolLevel );
        uint remainingSize = GetRemainingSize( protocolLevel );
        int bytesToWrite = (int)(headerSize + remainingSize.CompactByteCount() + 1);//Compute how many byte will be written.
        Span<byte> span = pw.GetSpan( bytesToWrite )[..bytesToWrite];
        span[0] = Header;
        span = span[1..].WriteVariableByteInteger( remainingSize );//the result span length will be HeaderSize.
        Debug.Assert( span.Length == headerSize );
        WriteHeaderContent( protocolLevel, span );
        pw.Advance( bytesToWrite );//advance the number of bytes written.
    }

    /// <summary>
    /// Write synchronously the header. This is intended to be used to write in memory data.
    /// </summary>
    /// <param name="span">The buffer to write to. It's length will be <see cref="GetHeaderSize()"/>.</param>
    protected abstract void WriteHeaderContent( ProtocolLevel protocolLevel, Span<byte> span );

    /// <summary>
    /// Write asynchronously the payload. This is intended to be used to write data that may not be in memory.
    /// </summary>
    /// <param name="pw">The <see cref="PipeWriter"/> to write to.</param>
    /// <param name="cancellationToken">The cancellation token, to cancel the write.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that complete with a <see cref="WriteResult"/> result.
    /// I recommend to watch it's documentation.</returns>
    protected abstract ValueTask WritePayloadAsync( ProtocolLevel protocolLevel, PipeWriter pw, CancellationToken cancellationToken );

    ///<inheritdoc/>
    public async ValueTask WriteAsync( ProtocolLevel protocolLevel, PipeWriter pw, CancellationToken cancellationToken )
    {
        WriteHeader( protocolLevel, pw );
        await WritePayloadAsync( protocolLevel, pw, cancellationToken );
        // Flush happen in the write loop.
    }
}
