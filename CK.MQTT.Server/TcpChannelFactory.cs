using CK.MQTT;
using CK.MQTT.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CK.LogHub
{
    class TcpChannelFactory : IMqttChannelFactory
    {
        readonly TcpListener _listener;
        public TcpChannelFactory( int port )
        {
            _listener = new( IPAddress.Any, port );
            _listener.Start();
        }

        public async ValueTask<(IMqttChannel channel, string connectionInfo)> CreateAsync( CancellationToken cancellationToken )
        {
            Console.WriteLine( "Waiting a connection..." );
            var client = await _listener.AcceptTcpClientAsync( cancellationToken );
            Console .WriteLine( "Client connected" );
            return (new ServerTcpChannel( client ), "");
        }

        public void Dispose() => _listener.Stop();
    }
}
