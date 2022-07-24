using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Represent the network connection.
    /// </summary>
    public interface IMQTTChannel : IDisposable
    {
        [MemberNotNull( nameof( DuplexPipe ) )] //after task completion.
        ValueTask StartAsync( CancellationToken cancellationToken );

        /// <summary>
        /// Disconnect the <see cref="IMQTTChannel"/>.
        /// </summary>
        /// <param name="m">The logger to use.</param>
        ValueTask CloseAsync( DisconnectReason closeReason );

        /// <summary>
        /// <see langword="true"/> if the channel was connected in the last operation on the <see cref="DuplexPipe"/>.
        /// </summary>
        public bool IsConnected { get; }

        /// <summary>
        /// Gets the stream.
        /// </summary>
        public IDuplexPipe? DuplexPipe { get; }
    }
}
