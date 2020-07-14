using System.IO;
using System.Net.Sockets;

namespace CK.MQTT
{
    /// <summary>
    /// Wrapper of <see cref="TcpClient"/> to <see cref="IMqttChannel"/>.
    /// </summary>
    public class TcpChannel : IMqttChannel
    {
        readonly TcpClient _tcpClient;

        /// <summary>
        /// Instantiate a new <see cref="TcpChannel"/>.
        /// The <paramref name="tcpClient"/> must be connected.
        /// </summary>
        /// <param name="tcpClient">The <see cref="TcpClient"/> to use.</param>
        public TcpChannel( TcpClient tcpClient )
        {
            _tcpClient = tcpClient;
        }

        /// <inheritdoc/>
        public bool IsConnected => _tcpClient.Connected;

        /// <inheritdoc/>
        public Stream Stream => _tcpClient.GetStream();

        /// <inheritdoc/>
        public void Close( IMqttLogger m ) => _tcpClient.Close();

        /// <inheritdoc/>
        public void Dispose() => _tcpClient.Dispose();
    }
}
