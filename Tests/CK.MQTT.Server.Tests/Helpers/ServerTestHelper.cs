using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.Server.Server;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Server.Tests.Helpers
{
    class ServerTestHelper : IAsyncDisposable
    {
        readonly int _port;
        readonly MqttDemiServer _server;
        public ServerTestHelper()
        {
            // When the debugger is attached, we use the default port.
            // Wireshark only detect mqtt on it's default port.
            var channelFactory = Debugger.IsAttached ? new TcpChannelFactory( 1883 ) : new TcpChannelFactory();
            _port = channelFactory.Port;
            var cfg = new Mqtt3ConfigurationBase();
            _server = new MqttDemiServer( cfg, channelFactory, new TestStoreFactory( cfg ), new TestAuthHandlerFactory() );
            _server.OnNewClient.Sync += OnNewClient;
            _server.StartListening();
        }

        TaskCompletionSource<MqttServerAgent>? _tcs;
        void OnNewClient( IActivityMonitor m, MqttServerAgent client )
        {
            _tcs?.SetResult( client );
            _tcs = null;
        }

        public async Task<(IConnectedMessageSender client, MqttServerAgent serverClient)> CreateClientAsync()
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
            res.Status.Should().Be( ConnectStatus.Successful ); ;
            var serverClient = await tcs.Task;
            return (client, serverClient);
        }

        public async ValueTask DisposeAsync() => await _server.StopListeningAsync();
    }
}
