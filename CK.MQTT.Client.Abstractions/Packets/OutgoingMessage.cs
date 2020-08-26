using System;
using System.Diagnostics;
using static CK.MQTT.ProtocolConfiguration;

namespace CK.MQTT
{
    /// <summary>
    /// Represent an outgoing mqtt message that will be sent.
    /// The dup flag is handled by the store transformer.
    /// </summary>
    public abstract class OutgoingMessage : ComplexOutgoingPacket, IOutgoingPacketWithId
    {
        readonly ProtocolLevelVersion _protocolLevel;
        readonly bool _retain;
        readonly string _topic;
        readonly string? _responseTopic;
        readonly ushort _correlationDataSize;
        readonly SpanLambda? _correlationDataWriter;
        readonly int _propertiesLength;
        /// <summary>
        /// Instantiate a new <see cref="OutgoingMessage"/>.
        /// </summary>
        /// <param name="retain">The retain flag.</param>
        /// <param name="topic">The message topic.</param>
        /// <param name="qos">The message qos.</param>
        protected OutgoingMessage(
            ProtocolLevelVersion protocolLevel, bool retain, string topic, QualityOfService qos,
            string? responseTopic, ushort correlationDataSize = 0, SpanLambda? correlationDataWriter = null //properties
        )
        {
            if( responseTopic != null )
            {
                if( protocolLevel == ProtocolLevelVersion.MQTT3 ) throw new ArgumentException( $"Cannot use properties in MQTT3. Property used: '{responseTopic}'." );
                _propertiesLength += 1 + responseTopic.MQTTSize();
            }
            if( correlationDataSize > 0 || correlationDataWriter != null )
            {
                if( correlationDataSize == 0 && correlationDataWriter != null ) throw new ArgumentException( $"{nameof( correlationDataSize )} is 0 but {nameof( correlationDataWriter )} is not null. If no data will be written, don't set the writer." );
                if( correlationDataWriter == null && correlationDataSize > 0 ) throw new ArgumentException( $"You set a {nameof( correlationDataSize )} but the {nameof( correlationDataWriter )} is null." );
                if( protocolLevel == ProtocolLevelVersion.MQTT3 ) throw new ArgumentException( $"Cannot use properties in MQTT3. Property used: correlation data." );
                _propertiesLength += 1 + correlationDataSize + 2;/*2 to write the data size itself*/
            }
            _protocolLevel = protocolLevel;
            _retain = retain;
            _topic = topic;
            Qos = qos;
            _responseTopic = responseTopic;
            _correlationDataSize = correlationDataSize;
            _correlationDataWriter = correlationDataWriter;
        }

        /// <inheritdoc/>
        public int PacketId { get; set; }

        /// <inheritdoc/>
        public QualityOfService Qos { get; }

        /// <inheritdoc/>
        protected sealed override int HeaderSize
            => _topic.MQTTSize()
            + (Qos > QualityOfService.AtMostOnce ? 2 : 0)//On QoS 0, no packet id(2bytes).
            + _protocolLevel switch
            {
                ProtocolLevelVersion.MQTT3 => 0,
                ProtocolLevelVersion.MQTT5 => _propertiesLength.CompactByteCount() + _propertiesLength,
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

        /// <summary>
        /// Write the topic, and the qos if qos>0.
        /// </summary>
        /// <param name="span"></param>
        protected sealed override void WriteHeaderContent( Span<byte> span )
        {
            span = span.WriteMQTTString( _topic );
            if( Qos > QualityOfService.AtMostOnce ) span = span.WriteUInt16( (ushort)PacketId );//topic id is not present on qos>0.
            if( _protocolLevel == ProtocolLevelVersion.MQTT5 )
            {
                span = span.WriteVariableByteInteger( _propertiesLength );
                if( _correlationDataWriter != null )
                {
                    span[0] = (byte)PropertyIdentifier.CorrelationData;
                    span = span[1..].WriteUInt16( _correlationDataSize );
                    _correlationDataWriter( span[.._correlationDataSize] );
                    span = span[_correlationDataSize..];
                }
                if( _responseTopic != null )
                {
                    span[0] = (byte)PropertyIdentifier.ResponseTopic;
                    span = span[1..].WriteMQTTString( _responseTopic );
                }
            }
            Debug.Assert( span.Length == 0 );
        }
    }
}
