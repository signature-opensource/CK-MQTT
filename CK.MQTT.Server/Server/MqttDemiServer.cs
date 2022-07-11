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

namespace CK.MQTT.Server.Server
{
    public class MqttDemiServer : MqttListenerBase
    {
        public MqttDemiServer( Mqtt3ConfigurationBase config,
                              IMqttChannelFactory channelFactory,
                              IStoreFactory storeFactory,
                              IAuthenticationProtocolHandlerFactory authenticationProtocolHandler )
            : base( config, channelFactory, storeFactory, authenticationProtocolHandler )
        {
            _config = config;
        }

        readonly PerfectEventSender<ServerMessageExchanger> _onNewClientSender = new();
        readonly Mqtt3ConfigurationBase _config;

        public PerfectEvent<ServerMessageExchanger> OnNewClient => _onNewClientSender.PerfectEvent;
        protected override async ValueTask CreateClientAsync(
            IActivityMonitor m,
            string clientId,
            IMqttChannel channel,
            IAuthenticationProtocolHandler securityManager,
            ILocalPacketStore localPacketStore,
            IRemotePacketStore remotePacketStore,
            IConnectInfo connectInfo, CancellationToken cancellationToken
        )
        {
            var exchanger = new ServerMessageExchanger(
                clientId,
                ProtocolConfiguration.FromProtocolLevel( connectInfo.ProtocolLevel ),
                _config,
                TODO,
                channel,
                remotePacketStore,
                localPacketStore
            );
            await _onNewClientSender.SafeRaiseAsync( m, exchanger );
        }
    }
}
