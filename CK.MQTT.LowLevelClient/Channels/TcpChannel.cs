using System;
using System.IO;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Wrapper of <see cref="TcpClient"/> to <see cref="IMQTTChannel"/>.
    /// </summary>
    public class TcpChannel : IMQTTChannel
    {
        readonly string _host;
        readonly int _port;

        TcpClient? _tcpClient;
        DuplexPipe? _duplexPipe;
        Stream? _stream;
        /// <summary>
        /// Instantiate a new <see cref="TcpChannel"/>.
        /// The <paramref name="tcpClient"/> must be connected.
        /// </summary>
        /// <param name="tcpClient">The <see cref="TcpClient"/> to use.</param>
        public TcpChannel( string host, int port )
        {
            _host = host;
            _port = port;
        }

        public async ValueTask StartAsync( CancellationToken cancellationToken )
        {
            if( _tcpClient != null ) throw new InvalidOperationException( "Already started." );
            _tcpClient = new TcpClient
            {
                NoDelay = true
            };
            await _tcpClient.ConnectAsync( _host, _port, cancellationToken );
            _stream = _tcpClient.GetStream();
            _duplexPipe = new DuplexPipe( PipeReader.Create( _stream ), PipeWriter.Create( _stream ) );
        }

        /// <inheritdoc/>
        public bool IsConnected => _tcpClient?.Connected ?? false;

        /// <inheritdoc/>
        public IDuplexPipe DuplexPipe => _duplexPipe ?? throw new InvalidOperationException( "Start the channel before accessing the pipes." );

        /// <inheritdoc/>
        public async ValueTask CloseAsync( DisconnectReason reason )
        {
            if( _tcpClient == null ) throw new InvalidOperationException( "Channel not started." );
            var stream = _stream;
            if( stream is not null )
            {
                await stream.DisposeAsync();
            }
            _tcpClient.Close();
            _tcpClient = null;
            _stream = null;
            _duplexPipe = null;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _duplexPipe?.Dispose();
            _stream?.Dispose();
            _tcpClient?.Dispose();
        }
    }
}
