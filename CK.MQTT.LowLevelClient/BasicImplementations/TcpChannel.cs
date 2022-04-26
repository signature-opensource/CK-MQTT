using Pipelines.Sockets.Unofficial;
using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Wrapper of <see cref="TcpClient"/> to <see cref="IMqttChannel"/>.
    /// </summary>
    public class TcpChannel : IMqttChannel
    {
        readonly EndPoint _endPoint;
        SocketConnection? _client;
        IDuplexPipe? _duplexPipe;
        /// <summary>
        /// Instantiate a new <see cref="TcpChannel"/>.
        /// The <paramref name="tcpClient"/> must be connected.
        /// </summary>
        /// <param name="tcpClient">The <see cref="TcpClient"/> to use.</param>
        public TcpChannel( EndPoint endPoint )
        {
            _endPoint = endPoint;
        }

        public async ValueTask StartAsync( CancellationToken cancellationToken )
        {
            if( _client != null ) throw new InvalidOperationException( "Already started." );
            _client = await SocketConnection.ConnectAsync( _endPoint );
            // TODO: Cancel auth.
            _duplexPipe = new DuplexPipe( _client.Input, _client.Output );
        }

        /// <inheritdoc/>
        public bool IsConnected => _client != null;

        /// <inheritdoc/>
        public IDuplexPipe DuplexPipe => _duplexPipe ?? throw new InvalidOperationException( "Start the channel before accessing the pipes." );

        /// <inheritdoc/>
        public void Close()
        {
            if( _client == null ) throw new InvalidOperationException( "Channel not started." );
            _client.TrySetProtocolShutdown( PipeShutdownKind.ProtocolExitClient );
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}
