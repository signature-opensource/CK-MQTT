using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Packets
{
    /// <summary>
    /// This represent the <a href="docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Will_Flag">last will</a>
    /// that will be sent inside the ConnectPacket.
    /// This is not a packet but a part of the connect packet.
    /// </summary>
    public abstract class OutgoingLastWill : IOutgoingPacket
    {
        public string Topic { get; }

        public bool IsRemoteOwnedPacketId => throw new NotSupportedException();

        /// <summary>
        /// Instantiate a new <see cref="OutgoingLastWill"/>.
        /// </summary>
        /// <param name="retain">The retain flag.</param>
        /// <param name="topic">The topic of the will message.</param>
        /// <param name="qos">The qos of the will message.</param>
        protected OutgoingLastWill( bool retain, string topic, QualityOfService qos )
        {
            MqttBinaryWriter.ThrowIfInvalidMQTTString( topic );
            Retain = retain;
            Qos = qos;
            Topic = topic;
        }

        /// <summary>
        /// This flag signal that the Client ask for this packet to be retained by the Broker.<br/>
        /// <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349234">See the specification for more details</a>.
        /// </summary>
        public bool Retain { get; }

        /// <summary>
        /// The qos of the message that will be emitted by the broker.
        /// <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349233">Specification link</a>
        /// </summary>
        public QualityOfService Qos { get; }
        public ushort PacketId { get; set; }

        /// <inheritdoc/>
        public abstract uint GetSize( ProtocolLevel protocolLevel );

        /// <summary>
        /// Should write the payload the the last will.
        /// </summary>
        /// <param name="writer">The <see cref="PipeWriter"/> to use.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to cancel the writing.</param>
        /// <returns></returns>
        protected abstract ValueTask<WriteResult> WritePayloadAsync( PipeWriter writer, CancellationToken cancellationToken );

        /// <summary>
        /// Write only the topic, then call <see cref="WritePayloadAsync(PipeWriter, CancellationToken)"/>.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async ValueTask<WriteResult> WriteAsync( ProtocolLevel protocolLevel, PipeWriter writer, CancellationToken cancellationToken )
        {
            int stringSize = (int)Topic.MQTTSize();
            writer.GetSpan( stringSize ).WriteMQTTString( Topic );
            writer.Advance( stringSize );
            await writer.FlushAsync( cancellationToken );
            return await WritePayloadAsync( writer, cancellationToken );
        }
    }
}
