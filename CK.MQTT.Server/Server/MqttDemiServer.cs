using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.Packets;
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
    public abstract class MqttDemiServer : MqttListener
    {
        public MqttDemiServer( Mqtt3ConfigurationBase config, IMqttChannelFactory channelFactory, IStoreFactory storeFactory )
            : base( config, channelFactory, storeFactory )
        {
            _config = config;
        }

        readonly PerfectEventSender<MessageExchangerAgent<IConnectedMessageExchanger>> _onNewClientSender = new();
        readonly Mqtt3ConfigurationBase _config;

        public PerfectEvent<MessageExchangerAgent<IConnectedMessageExchanger>> OnNewClient => _onNewClientSender.PerfectEvent;
        protected override async ValueTask CreateClientAsync(
            IActivityMonitor m,
            IMqttChannel channel,
            IAuthenticationProtocolHandler securityManager,
            ILocalPacketStore localPacketStore,
            IRemotePacketStore remotePacketStore,
            IConnectInfo connectInfo,
            CancellationToken cancellationToken
        )
        {
            await _onNewClientSender.SafeRaiseAsync( m, new MessageExchangerAgent<IConnectedMessageExchanger>(
                ( sink ) =>
                {
                    return new MessageExchanger(
                        ProtocolConfiguration.FromProtocolLevel( connectInfo.ProtocolLevel ),
                        _config,
                        sink,
                        channel,
                        remotePacketStore,
                        localPacketStore
                    );
                }
            ) );
        }
    }
}