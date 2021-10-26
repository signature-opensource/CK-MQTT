using CK.Core;
using CK.MQTT.Common.BasicImplementations;
using CK.MQTT.P2P;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
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

        public ValueTask<IMqttChannel> CreateAsync( IActivityMonitor m, string connectionString )
         => new( _clientChannel );
    }
}
