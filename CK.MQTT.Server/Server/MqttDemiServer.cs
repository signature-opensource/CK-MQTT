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
    public class MqttDemiServer : MqttListener
    {
        public MqttDemiServer( Mqtt3ConfigurationBase config,
                              IMqttChannelFactory channelFactory,
                              IStoreFactory storeFactory,
                              IAuthenticationProtocolHandlerFactory authenticationProtocolHandler )
            : base( config, channelFactory, storeFactory, authenticationProtocolHandler )
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
            var exchanger = new MessageExchangerAgent<IConnectedMessageExchanger>(
                ( sink ) =>
                {
                    ((MessageExchangerAgent<IConnectedMessageExchanger>)sink).Start();
                    return new ServerMessageExchanger(
                        ProtocolConfiguration.FromProtocolLevel( connectInfo.ProtocolLevel ),
                        _config,
                        sink,
                        channel,
                        new SimpleTopicManager(),
                        remotePacketStore,
                        localPacketStore
                    );
                }
            );
            exchanger.Start();
            await _onNewClientSender.SafeRaiseAsync( m, exchanger );
        }
    }
}
