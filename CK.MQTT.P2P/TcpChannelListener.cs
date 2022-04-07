using CK.MQTT.Common.BasicImplementations;
using System;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.P2P
{
    public class TcpChannelListener : IMqttChannel
    {
        readonly TcpListener _listener;
        StreamChannel? _channel;
        public TcpChannelListener( TcpListener listener )
        {
            _listener = listener;
            _listener.Start();
        }

        public bool IsConnected => _channel?.IsConnected ?? false;

        public IDuplexPipe DuplexPipe => _channel?.DuplexPipe ?? throw new InvalidOperationException();

        public void Close()
        {
            _listener.Stop();
        }

        public void Dispose()
        {
            _channel?.Dispose();
        }

        public async ValueTask StartAsync()
        {
            TcpClient client = await _listener.AcceptTcpClientAsync();
            _channel = new StreamChannel( client.GetStream() );
        }
    }
}
