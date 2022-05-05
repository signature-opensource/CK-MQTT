using CK.MQTT;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace CK.LogHub
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

        public void Close() => _tcpClient.Close();

        public void Dispose() => _tcpClient.Dispose();

        public ValueTask StartAsync( CancellationToken cancellationToken )
            => new ValueTask();
    }
}
