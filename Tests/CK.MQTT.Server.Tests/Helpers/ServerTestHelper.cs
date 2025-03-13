using CK.Core;
using CK.MQTT.Client;
using Shouldly;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.MQTT.Server.Tests.Helpers;

class ServerTestHelper : IAsyncDisposable
{
    readonly int _port;
    readonly MQTTDemiServer _server;
    public ServerTestHelper()
    {
        // When the debugger is attached, we use the default port.
        // Wireshark only detect mqtt on it's default port.
        var channelFactory = Debugger.IsAttached ? new TcpChannelFactory( 1883 ) : new TcpChannelFactory();
        _port = channelFactory.Port;
        var cfg = new MQTT3ConfigurationBase();
        _server = new MQTTDemiServer( cfg, channelFactory, new TestStoreFactory( cfg ), new TestAuthHandlerFactory() );
        _server.OnNewClient.Sync += OnNewClient;
        _server.StartListening();
    }

    TaskCompletionSource<MQTTServerAgent>? _tcs;
    void OnNewClient( IActivityMonitor m, MQTTServerAgent client )
    {
        _tcs?.SetResult( client );
        _tcs = null;
    }

    public async Task<(IConnectedMessageSender client, MQTTServerAgent serverClient)> CreateClientAsync()
    {
        var messageWorker = new MessageWorker();
        var sink = new DefaultClientMessageSink( messageWorker.QueueMessage );
        var client = new LowLevelMQTTClient(
                ProtocolConfiguration.MQTT3,
                new MQTT3ClientConfiguration()
                {
                    WaitTimeoutMilliseconds = 50_000,
                    KeepAliveSeconds = 0
                },
                sink,
                new TcpChannel( "localhost", _port )
            );
        var agent = new MQTTClientAgent( client, messageWorker );
        var tcs = _tcs = new();
        var res = await agent.ConnectAsync( true );
        res.Status.ShouldBe( ConnectStatus.Successful ); ;
        var serverClient = await tcs.Task;
        return (client, serverClient);
    }

    public async ValueTask DisposeAsync() => await _server.StopListeningAsync();
}
