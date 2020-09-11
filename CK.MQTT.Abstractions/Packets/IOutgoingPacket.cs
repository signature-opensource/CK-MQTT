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
        /// Result of a Write Operation.
        /// </summary>
        public enum WriteResult
        {
            /// <summary>
            /// The <see cref="IOutgoingPacket"/> is expired. No write operation has been made.
            /// </summary>
            Expired,
            /// <summary>
            /// The <see cref="IOutgoingPacket"/> has been written. It may or may not be reused.
            /// </summary>
            Written,
            /// <summary>
            /// The write has been cancelled.
            /// </summary>
            Cancelled
        }

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
        int GetSize( ProtocolLevel protocolLevel );
    }
}
