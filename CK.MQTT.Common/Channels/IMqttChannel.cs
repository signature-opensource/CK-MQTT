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
    public interface IMqttChannel : IDisposable
    {
        [MemberNotNull( nameof( DuplexPipe ) )] //after task completion.
        ValueTask StartAsync( CancellationToken cancellationToken );

        /// <summary>
        /// Disconnect the <see cref="IMqttChannel"/>.
        /// </summary>
        /// <param name="m">The logger to use.</param>
        void Close();

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
