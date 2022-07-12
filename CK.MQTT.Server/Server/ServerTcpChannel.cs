using System;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    class ServerTcpChannel : IMqttChannel
    {
        readonly TcpClient _tcpClient;
        readonly DuplexPipe _duplexPipe;
        public ServerTcpChannel( TcpClient tcpClient )
        {
            _tcpClient = tcpClient;
            var stream = _tcpClient.GetStream();
            _duplexPipe = new DuplexPipe( PipeReader.Create( stream ), PipeWriter.Create( stream ) );
        }
        public bool IsConnected => _tcpClient.Connected;

        public IDuplexPipe? DuplexPipe => _duplexPipe;

        bool _closed;
        public ValueTask CloseAsync( DisconnectReason reason )
        {
            _closed = true;
            _tcpClient.Close();
            return new ValueTask();
        }

        public void Dispose() => _tcpClient.Dispose();


        public ValueTask StartAsync( CancellationToken cancellationToken )
        {
            if( _closed ) throw new InvalidOperationException( "This channel cannot be restarted." );
            return new();
        }
    }
}
