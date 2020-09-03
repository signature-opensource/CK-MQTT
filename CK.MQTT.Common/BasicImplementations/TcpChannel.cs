using System.IO;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace CK.MQTT
{
    /// <summary>
    /// Wrapper of <see cref="TcpClient"/> to <see cref="IMqttChannel"/>.
    /// </summary>
    public class TcpChannel : IMqttChannel
    {
        readonly TcpClient _tcpClient;
        readonly DuplexPipe _duplexPipe;

        /// <summary>
        /// Instantiate a new <see cref="TcpChannel"/>.
        /// The <paramref name="tcpClient"/> must be connected.
        /// </summary>
        /// <param name="tcpClient">The <see cref="TcpClient"/> to use.</param>
        public TcpChannel( TcpClient tcpClient, StreamPipeReaderOptions? readerOptions = null, StreamPipeWriterOptions? writerOptions = null )
        {
            _tcpClient = tcpClient;
            _duplexPipe = new DuplexPipe( tcpClient.GetStream(), readerOptions, writerOptions );
        }

        /// <inheritdoc/>
        public bool IsConnected => _tcpClient.Connected;

        /// <inheritdoc/>
        public IDuplexPipe DuplexPipe => _duplexPipe;

        /// <inheritdoc/>
        public void Close( IInputLogger? m ) => _tcpClient.Close();

        /// <inheritdoc/>
        public void Dispose()
        {
            _duplexPipe.Dispose();
            _tcpClient.Dispose();
        }
    }
}
