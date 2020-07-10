using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// A packet that will be sent.
    /// </summary>
    public interface IOutgoingPacket
    {
        /// <summary>
        /// Write asynchronously on a <see cref="PipeWriter"/> the packet.
        /// </summary>
        /// <param name="writer">The <see cref="PipeWriter"/> to write to.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel writting.</param>
        /// <returns></returns>
        ValueTask<bool> WriteAsync( PipeWriter writer, CancellationToken cancellationToken );

        /// <summary>
        /// The <see cref="Size"/> of the packet. May be used by stores to allocate the required space to store the packet.
        /// </summary>
        int Size { get; }
    }
}
