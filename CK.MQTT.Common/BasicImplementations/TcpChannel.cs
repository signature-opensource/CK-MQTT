using CK.Core;
using System;
using System.IO;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Wrapper of <see cref="TcpClient"/> to <see cref="IMqttChannel"/>.
    /// </summary>
    public class TcpChannel : IMqttChannel
    {

        readonly string _host;
        readonly int _port;

        TcpClient _tcpClient = null!;
        DuplexPipe _duplexPipe = null!;

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

        public async ValueTask StartAsync( IActivityMonitor? m )
        {
            _tcpClient = new TcpClient
            {
                NoDelay = true
            };
            await _tcpClient.ConnectAsync( _host, _port );
            Stream stream = _tcpClient.GetStream();
            _duplexPipe = new DuplexPipe( PipeReader.Create( stream ), PipeWriter.Create( stream ) );
        }

        /// <inheritdoc/>
        public bool IsConnected => _tcpClient.Connected;

        /// <inheritdoc/>
        public IDuplexPipe DuplexPipe => _duplexPipe ?? throw new InvalidOperationException( "Start the channel before accessing the pipes." );

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
