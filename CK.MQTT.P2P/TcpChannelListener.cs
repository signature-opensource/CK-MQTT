using CK.MQTT.Common.BasicImplementations;
using CK.MQTT.Server;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.P2P
{
    public class TcpChannelListener : IMqttChannelListener, IDisposable
    {
        readonly TcpListener _listener;

        public TcpChannelListener( TcpListener listener )
        {
            _listener = listener;
            _listener.Start();
        }

        public async Task<(IMqttChannel channel, string clientAddress)> AcceptIncomingConnection( CancellationToken cancellationToken )
        {
            TcpClient client = await _listener.AcceptTcpClientAsync();

            return (new StreamChannel( client.GetStream() ), ""); //TODO: this should be the client address.
        }

        public void Dispose() => _listener.Stop();
    }
}
