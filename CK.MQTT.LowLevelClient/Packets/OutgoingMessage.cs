using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Packets;

/// <summary>
/// Represent an outgoing mqtt message that will be sent.
/// The dup flag is handled by the store transformer.
/// </summary>
public abstract class OutgoingMessage : ComplexOutgoingPacket, IAsyncDisposable
{
    readonly bool _retain;
    readonly string? _responseTopic;
    readonly ushort _correlationDataSize;
    readonly SpanAction? _correlationDataWriter;
    readonly uint _propertiesLength;
    /// <summary>
    /// Instantiate a new <see cref="OutgoingMessage"/>.
    /// </summary>
    /// <param name="retain">The retain flag.</param>
    /// <param name="topic">The message topic.</param>
    /// <param name="qos">The message qos.</param>
    protected OutgoingMessage(
        string topic, QualityOfService qos, bool retain,
        string? responseTopic = null, ushort correlationDataSize = 0, SpanAction? correlationDataWriter = null
    ) //properties 
    {
        MQTTBinaryWriter.ThrowIfInvalidMQTTString( topic );
        //Compute properties size.
        if( responseTopic != null )
        {
            _propertiesLength += 1 + responseTopic.MQTTSize();
        }
        if( correlationDataSize > 0 || correlationDataWriter != null )
        {
            if( correlationDataSize == 0 && correlationDataWriter != null ) throw new ArgumentException( $"{nameof( correlationDataSize )} is 0 but {nameof( correlationDataWriter )} is not null. If no data will be written, don't set the writer." );
            if( correlationDataWriter == null && correlationDataSize > 0 ) throw new ArgumentException( $"You set a {nameof( correlationDataSize )} but the {nameof( correlationDataWriter )} is null." );
            _propertiesLength += 1u + correlationDataSize + 2;/*2 to write the data size itself*/
        }
        //Assignation
        _retain = retain;
        Topic = topic;
        Qos = qos;
        _responseTopic = responseTopic;
        _correlationDataSize = correlationDataSize;
        _correlationDataWriter = correlationDataWriter;
    }
    public string Topic { get; }

    public override bool IsRemoteOwnedPacketId => false;
    ushort _packetId = 0;

    /// <inheritdoc/>
    public override ushort PacketId
    {
        get
        {
            Debug.Assert( _packetId != 0, "Packet ID should be intialized before read." );
            return _packetId;
        }
        set
        {
            Debug.Assert( value != 0 );
            _packetId = value;
        }
    }

    public sealed override PacketType Type => PacketType.Publish;

    /// <inheritdoc/>
    public override QualityOfService Qos { get; }

    /// <inheritdoc/>
    protected sealed override uint GetHeaderSize( ProtocolLevel protocolLevel )
        => Topic.MQTTSize()
            + (Qos > QualityOfService.AtMostOnce ? 2u : 0)//On QoS 0, no packet id(2bytes).
            + protocolLevel switch
            {
                ProtocolLevel.MQTT3 => 0,
                ProtocolLevel.MQTT5 => _propertiesLength.CompactByteCount() + _propertiesLength,
                _ => throw new InvalidOperationException( "Unknown protocol level" )
            };

    const byte _retainFlag = 1;

    /// <summary>
    /// First byte of the packet.
    /// <see cref="PacketType"/> is stored on the 4 left bits, then, from left to right,
    /// the dup flag, then the qos(2 bits) the retain flags.
    /// </summary>
    protected sealed override byte Header =>
        (byte)(
            (byte)PacketType.Publish |
            (byte)Qos << 1 |
            (byte)(_retain ? _retainFlag : 0)
        );



    protected abstract uint PayloadSize { get; }
    protected sealed override uint GetPayloadSize( ProtocolLevel protocolLevel ) => PayloadSize;

    /// <summary>
    /// Write the topic, and the qos if qos>0.
    /// </summary>
    /// <param name="span"></param>
    protected override void WriteHeaderContent( ProtocolLevel protocolLevel, Span<byte> span )
    {
        span = span.WriteMQTTString( Topic );
        if( Qos > QualityOfService.AtMostOnce )
        {
            BinaryPrimitives.WriteUInt16BigEndian( span, (ushort)PacketId ); //packet id is not present on qos=0.
            span = span[2..];
        }

        if( protocolLevel == ProtocolLevel.MQTT3 )
        {
            if( _propertiesLength > 0 ) throw new InvalidOperationException( "Sending a MQTT5 Publish when agent is running in MQTT3." );
        }
        else if( protocolLevel == ProtocolLevel.MQTT5 )
        {
            span = span.WriteVariableByteInteger( _propertiesLength );
            if( _correlationDataWriter != null )
            {
                span[0] = (byte)PropertyIdentifier.CorrelationData;
                span = span[1..];
                BinaryPrimitives.WriteUInt16BigEndian( span, _correlationDataSize );
                span = span[2..];
                _correlationDataWriter( span[.._correlationDataSize] );
                span = span[_correlationDataSize..];
            }
            if( _responseTopic != null )
            {
                span[0] = (byte)PropertyIdentifier.ResponseTopic;
                span[1..].WriteMQTTString( _responseTopic );
            }
        }
    }

    protected abstract ValueTask WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken );

    protected sealed override ValueTask WritePayloadAsync( ProtocolLevel protocolLevel, PipeWriter pw, CancellationToken cancellationToken )
        => WritePayloadAsync( pw, cancellationToken );
    public abstract ValueTask DisposeAsync();
}
