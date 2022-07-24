using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.Stores;
using CK.PerfectEvent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
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
            var agent = new MQTTServerAgent( ( sink ) =>
            new ServerMessageExchanger(
                clientId,
                ProtocolConfiguration.FromProtocolLevel( connectInfo.ProtocolLevel ),
                Config,
                sink,
                channel,
                remotePacketStore,
                localPacketStore
            ) );
            await _onNewClientSender.SafeRaiseAsync( m, agent );
        }
    }
}
