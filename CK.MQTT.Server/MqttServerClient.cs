using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.P2P;
using CK.MQTT.Packets;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public class MqttServerClient : MqttListener, IMqtt3Client
    {
        TaskCompletionSource<(IMqttChannel channel, ISecurityManager securityManager, ILocalPacketStore localPacketStore, IRemotePacketStore remotePacketStore, IConnectInfo connectInfo)>? _needClientTCS;
        MessageExchanger? _exchanger;
        public MqttServerClient( Mqtt3ConfigurationBase config, IMqttChannelFactory channelFactory, IStoreFactory storeFactory, ISecurityManagerFactory securityManagerFactory ) : base( config, channelFactory, storeFactory )
        {
            SecurityManagerFactory = new SecurityManagerFactoryWrapper( this, securityManagerFactory );
        }

        protected override ISecurityManagerFactory SecurityManagerFactory { get; }

        class SecurityManagerFactoryWrapper : ISecurityManagerFactory
        {
            readonly MqttServerClient _client;
            readonly ISecurityManagerFactory _securityManagerFactory;

            public SecurityManagerFactoryWrapper( MqttServerClient client, ISecurityManagerFactory securityManagerFactory )
            {
                _client = client;
                _securityManagerFactory = securityManagerFactory;
            }

            public async ValueTask<ISecurityManager?> ChallengeIncomingConnectionAsync( string connectionInfo, CancellationToken cancellationToken )
            {
                if( _client._needClientTCS == null ) return null; //Deny all connection when we dont need a client.
                return await _securityManagerFactory.ChallengeIncomingConnectionAsync( connectionInfo, cancellationToken );
            }
        }

        class ClientWrapper : ServerMessageExchanger
        {
            public ClientWrapper( ProtocolConfiguration pConfig, Mqtt3ConfigurationBase config, IMqtt3Sink sink, IMqttChannel channel, IRemotePacketStore? remotePacketStore = null, ILocalPacketStore? localPacketStore = null ) : base( pConfig, config, sink, channel, remotePacketStore, localPacketStore )
            {
            }

            protected override InputPump CreateInputPump( Reflex reflex )
            {
                new InterlacedInputPump(
                    Sink,
                    _topicFilter,
                    SelfDisconnectAsync, Config, , connectReflex.HandleRequestAsync,
                    _subscriptionsCommand.Reader
                )
            }
        }

        protected override ValueTask CreateClientAsync( IActivityMonitor m, IMqttChannel channel, ISecurityManager securityManager, ILocalPacketStore localPacketStore, IRemotePacketStore remotePacketStore, IConnectInfo connectInfo, CancellationToken cancellationToken )
        {
            _needClientTCS!.SetResult( (channel, securityManager, localPacketStore, remotePacketStore, connectInfo) );
            return new ValueTask();
        }

        public Task<ConnectResult> ConnectAsync( OutgoingLastWill? lastWill = null, CancellationToken cancellationToken = default )
        {
            if( lastWill != null ) throw new ArgumentException( "Last will is not supported by a P2P client." );
            if( _exchanger?.IsConnected ?? false ) throw new InvalidOperationException( "This client is already connected." );


        }

        public ValueTask<Task> UnsubscribeAsync( params string[] topics )
        {
            throw new NotImplementedException();
        }

        public ValueTask<Task<SubscribeReturnCode[]>> SubscribeAsync( IEnumerable<Subscription> subscriptions )
        {
            throw new NotImplementedException();
        }

        public ValueTask<Task<SubscribeReturnCode>> SubscribeAsync( Subscription subscriptions )
        {
            throw new NotImplementedException();
        }

        public Task<bool> DisconnectAsync( bool deleteSession )
        {
            throw new NotImplementedException();
        }

        public ValueTask<Task> PublishAsync( OutgoingMessage message )
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
