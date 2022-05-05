using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public class TcpChannelFactory : IMqttChannelFactory
    {
        readonly TcpListener _listener;
        public TcpChannelFactory( int port )
        {
            _listener = new( IPAddress.Any, port );
            _listener.Start();
            Port = port;
        }
        public TcpChannelFactory()
        {
            _listener = new TcpListener( IPAddress.Any, 0 );
            _listener.Start();
            Port = ((IPEndPoint)_listener.Server.LocalEndPoint!).Port;
        }

        public int Port { get; }

        public async ValueTask<(IMqttChannel channel, string connectionInfo)> CreateAsync( CancellationToken cancellationToken )
        {
            Console.WriteLine( "Waiting a connection..." );
            var client = await _listener.AcceptTcpClientAsync( cancellationToken );
            Console.WriteLine( "Client connected" );
            return (new ServerTcpChannel( client ), "");
        }

        public void Dispose() => _listener.Stop();
    }
}
