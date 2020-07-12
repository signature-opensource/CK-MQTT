using System;

namespace CK.MQTT
{
    /// <summary>
    /// Represent an outgoing mqtt message that will be sent.
    /// </summary>
    public abstract class OutgoingApplicationMessage : ComplexOutgoingPacket, IOutgoingPacketWithId
    {
        readonly bool _dup;
        readonly bool _retain;
        /// <summary>
        /// Instantiate a new <see cref="OutgoingApplicationMessage"/>.
        /// </summary>
        /// <param name="dup">The dup flag.</param>
        /// <param name="retain">The retain flag.</param>
        /// <param name="topic">The message topic.</param>
        /// <param name="qos">The message qos.</param>
        protected OutgoingApplicationMessage( bool dup, bool retain, string topic, QualityOfService qos )
        {
            _dup = dup;
            _retain = retain;
            Topic = topic;
            Qos = qos;
        }

        /// <summary>
        /// The topic of the message.
        /// </summary>
        public string Topic { get; set; }

        /// <inheritdoc/>
        public int PacketId { get; set; }

        /// <inheritdoc/>
        public QualityOfService Qos { get; }

        /// <inheritdoc/>
        protected sealed override int HeaderSize => Topic.MQTTSize() + (Qos > QualityOfService.AtMostOnce ? 2 : 0);//On QoS 0, no packet id(2bytes).

        const byte _dupFlag = 1 << 4;
        const byte _retainFlag = 1;

        /// <summary>
        /// First byte of the packet.
        /// <see cref="PacketType"/> is stored on the 4 left bits, then, from left to right,
        /// the dup flag, then the qos(2 bits) the retain flags.
        /// </summary>
        protected sealed override byte Header =>
            (byte)(
                (byte)PacketType.Publish |
                (byte)(_dup ? _dupFlag : 0) |
                (byte)Qos << 1 |
                (byte)(_retain ? _retainFlag : 0)
            );

        /// <summary>
        /// Write the topic, and the qos if qos>0.
        /// </summary>
        /// <param name="span"></param>
        protected sealed override void WriteHeaderContent( Span<byte> span )
        {
            span = span.WriteMQTTString( Topic );
            if( Qos > QualityOfService.AtMostOnce ) span.WriteUInt16( (ushort)PacketId );//topic id is not present on qos>0.
        }
    }
}
