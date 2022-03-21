using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Packets
{
    /// <summary>
    /// A packet that will be sent.
    /// </summary>
    public interface IOutgoingPacket
    {
        /// <summary>
        /// The packet id of the <see cref="IOutgoingPacket"/>. 0 when <see cref="Qos"/> is <see cref="QualityOfService.AtMostOnce"/>.
        /// <a href="docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349268">
        /// Read the specification fore more information</a>.
        /// </summary>
        ushort PacketId { get; set; }

        /// <summary>
        /// The QoS of the packet. A packet with an identifier is never at QoS 0.
        /// </summary>
        QualityOfService Qos { get; }

        bool IsRemoteOwnedPacketId { get; }

        /// <summary>
        /// Ask to write asynchronously on a <see cref="PipeWriter"/> the packet.
        /// </summary>
        /// <param name="writer">The <see cref="PipeWriter"/> to write to.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel writting.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that complete when the packet is written.
        /// It's Result is a <see cref="WriteResult"/> that is <see cref="WriteResult.Written"/> if packet was written,
        /// or <see cref="WriteResult.Expired"/> if the packed was expired and could not be written.</returns>
        ValueTask<WriteResult> WriteAsync( ProtocolLevel protocolLevel, PipeWriter writer, CancellationToken cancellationToken );

        /// <summary>
        /// The Size of the packet. May be used by stores to allocate the required space to store the packet.
        /// </summary>
        uint GetSize( ProtocolLevel protocolLevel );
    }
}
