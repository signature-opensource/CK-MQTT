using CK.MQTT.Common.BasicImplementations;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.P2P.Tests
{
    class LocalLoopbackTestChannel : IMqttChannelListener, IMqttChannelFactory
    {
        readonly IMqttChannel _serverChannel;
        readonly IMqttChannel _clientChannel;
        public LocalLoopbackTestChannel()
        {
            var clientWrite = new MemoryStream();
            var serverWrite = new MemoryStream();
            _serverChannel = new StreamChannel( clientWrite, serverWrite );
            _clientChannel = new StreamChannel( serverWrite, clientWrite );
        }

        public Task<(IMqttChannel channel, string clientAddress)> AcceptIncomingConnection( CancellationToken cancellationToken )
            => Task.FromResult( (_serverChannel, "loopback") );

        public ValueTask<IMqttChannel> CreateAsync( string connectionString )
         => new( _clientChannel );
    }
}
