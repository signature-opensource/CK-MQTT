using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.Server.Server;
using CK.MQTT.Stores;
using CK.PerfectEvent;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server;

public class MQTTDemiServer : MQTTListenerBase
{
    public MQTTDemiServer( MQTT3ConfigurationBase config,
                          IMQTTChannelFactory channelFactory,
                          IStoreFactory storeFactory,
                          IAuthenticationProtocolHandlerFactory authenticationProtocolHandler )
        : base( config, channelFactory, storeFactory, authenticationProtocolHandler )
    {
    }

    readonly PerfectEventSender<MQTTServerAgent> _onNewClientSender = new();

    public PerfectEvent<MQTTServerAgent> OnNewClient => _onNewClientSender.PerfectEvent;
    protected override async ValueTask CreateClientAsync(
        IActivityMonitor m,
        string clientId,
        IMQTTChannel channel,
        IAuthenticationProtocolHandler securityManager,
        ILocalPacketStore localPacketStore,
        IRemotePacketStore remotePacketStore,
        IConnectInfo connectInfo, CancellationToken cancellationToken
    )
    {
        var messageWorker = new MessageWorker();
        var serverSink = new ServerClientMessageSink( messageWorker.QueueMessage );
        var exchanger = new ServerMessageExchanger(
            clientId,
            ProtocolConfiguration.FromProtocolLevel( connectInfo.ProtocolLevel ),
            Config,
            serverSink,
            channel,
            remotePacketStore,
            localPacketStore
        );
        var agent = new MQTTServerAgent( exchanger, messageWorker, clientId );

        await _onNewClientSender.SafeRaiseAsync( m, agent );
    }
}
