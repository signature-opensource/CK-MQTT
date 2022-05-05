using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.Server.Server;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Server.Tests
{
    class ServerTestHelper : IAsyncDisposable
    {
        readonly int _port;
        readonly MqttDemiServer _server;
        public ServerTestHelper()
        {
            var channelFactory = new TcpChannelFactory(1883);
            _port = channelFactory.Port;
            var cfg = new Mqtt3ConfigurationBase();
            _server = new MqttDemiServer( cfg, channelFactory, new TestStoreFactory( cfg ), new TestAuthHandlerFactory() );
            _server.OnNewClient.Sync += OnNewClient;
            _server.StartListening();
        }

        TaskCompletionSource<IConnectedMessageExchanger>? _tcs;
        void OnNewClient( IActivityMonitor m, IConnectedMessageExchanger client )
        {
            _tcs?.SetResult( client );
            _tcs = null;
        }

        public async Task<(IConnectedMessageExchanger client, IConnectedMessageExchanger serverClient)> CreateClient()
        {
            var client = new MqttClientAgent(
                ( sink ) => new LowLevelMqttClient(
                    ProtocolConfiguration.Mqtt3,
                    new Mqtt3ClientConfiguration()
                    {
                        WaitTimeoutMilliseconds = 50_000,
                        KeepAliveSeconds = 0
                    },
                    sink,
                    new TcpChannel( "localhost", _port )
                )
            );
            var tcs = _tcs = new();
            var res = await client.ConnectAsync();
            res.IsSuccess.Should().BeTrue();
            var serverClient = await tcs.Task;
            return (client, serverClient);
        }


        public async ValueTask DisposeAsync() => await _server.StopListeningAsync();
    }
}
