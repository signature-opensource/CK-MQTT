using CK.Core;
using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Represent the network connection.
    /// </summary>
    public interface IMqttChannel : IDisposable
    {
        ValueTask StartAsync( IActivityMonitor? m );

        /// <summary>
        /// Disconnect the <see cref="IMqttChannel"/>.
        /// </summary>
        /// <param name="m">The logger to use.</param>
        void Close( IInputLogger? m );

        /// <summary>
        /// <see langword="true"/> if the channel was connected in the last operation on the <see cref="DuplexPipe"/>.
        /// </summary>
        public bool IsConnected { get; }

        /// <summary>
        /// Gets the stream.
        /// </summary>
        public IDuplexPipe DuplexPipe { get; }
    }
}
