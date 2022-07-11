using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.Packets;
using CK.MQTT.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Server.ServerClient
{
    public class MqttServerClient : MqttListenerBase, IMqtt3Client, IAsyncDisposable
    {
        public IMqttServerSink Sink { get; set; }
        internal TaskCompletionSource<(IMqttChannel channel, IAuthenticationProtocolHandler securityManager, ILocalPacketStore localPacketStore, IRemotePacketStore remotePacketStore, IConnectInfo connectInfo)>? _needClientTCS;
        ServerMessageExchanger? _wrapper;

        public string? ClientId => _wrapper?.ClientId;

        public MqttServerClient( Mqtt3ConfigurationBase config, IMqttServerSink sink, IMqttChannelFactory channelFactory, IStoreFactory storeFactory, IAuthenticationProtocolHandlerFactory securityManagerFactory )
            : base( config, channelFactory, storeFactory, securityManagerFactory )
        {
            AuthProtocolHandlerFactory = new SecurityManagerFactoryWrapper( this, securityManagerFactory );
            Sink = sink;
        }

        protected override ValueTask CreateClientAsync( IActivityMonitor m, string clientId, IMqttChannel channel, IAuthenticationProtocolHandler securityManager, ILocalPacketStore localPacketStore, IRemotePacketStore remotePacketStore, IConnectInfo connectInfo, CancellationToken cancellationToken )
        {
            _needClientTCS!.SetResult( (channel, securityManager, localPacketStore, remotePacketStore, connectInfo) );
            _needClientTCS = null;
            return new ValueTask();
        }

        public async Task<ConnectResult> ConnectAsync( OutgoingLastWill? lastWill = null, CancellationToken cancellationToken = default )
        {
            if( lastWill != null ) throw new ArgumentException( "Last will is not supported by a P2P client." );
            if( _wrapper?.IsConnected ?? false ) throw new InvalidOperationException( "This client is already connected." );
            var tcs = new TaskCompletionSource<(IMqttChannel channel, IAuthenticationProtocolHandler securityManager, ILocalPacketStore localPacketStore, IRemotePacketStore remotePacketStore, IConnectInfo connectInfo)>();
            _needClientTCS = tcs;
            var (channel, _, localPacketStore, remotePacketStore, connectInfo) = await tcs.Task;
            _wrapper = new ServerMessageExchanger( connectInfo.ClientId,
                ProtocolConfiguration.FromProtocolLevel( connectInfo.ProtocolLevel ),
                Config, Sink, channel, remotePacketStore, localPacketStore );
            return new ConnectResult( localPacketStore.IsRevivedSession ? SessionState.SessionPresent : SessionState.CleanSession, ProtocolConnectReturnCode.Accepted );
        }

        public ValueTask<Task> UnsubscribeAsync( params string[] topics )
            => new ValueTask<Task>( Task.CompletedTask );

        public ValueTask<Task<SubscribeReturnCode[]>> SubscribeAsync( IEnumerable<Subscription> subscriptions )
        {
            return new( Task.FromResult(
                new SubscribeReturnCode[subscriptions.Count()]
            // We cannot ask the client a certain QoS as it's a fake subscribe, we return 0 because we canno't make the guarentee the QoS will be higher.
            ) );
        }

        public ValueTask<Task<SubscribeReturnCode>> SubscribeAsync( Subscription subscriptions )
        {
            return new( Task.FromResult(
                SubscribeReturnCode.MaximumQoS0
            // We cannot ask the client a certain QoS as it's a fake subscribe, we return 0 because we canno't make the guarentee the QoS will be higher.
            ) );
        }

        public Task<bool> DisconnectAsync( bool deleteSession )
            => _wrapper!.DisconnectAsync( deleteSession );

        public ValueTask<Task> PublishAsync( OutgoingMessage message )
            => _wrapper!.PublishAsync( message );

        public async ValueTask DisposeAsync()
        {
            if( _wrapper != null )
            {
                await _wrapper.DisposeAsync();
            }
        }
    }
}
