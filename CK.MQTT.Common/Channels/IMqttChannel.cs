using System;
using System.IO;

namespace CK.MQTT
{
    /// <summary>
    /// Represent the network connection.
    /// </summary>
    public interface IMqttChannel : IDisposable
    {
        /// <summary>
        /// Disconnect the <see cref="IMqttChannel"/>.
        /// </summary>
        /// <param name="m">The logger to use.</param>
        void Close( IMqttLogger m );

        /// <summary>
        /// <see langword="true"/> if the channel was connected in the last operation on the <see cref="Stream"/>.
        /// </summary>
        public bool IsConnected { get; }

        /// <summary>
        /// Gets the stream.
        /// </summary>
        public Stream Stream { get; }
    }
}
