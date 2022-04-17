using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.P2P;
using CK.MQTT.Packets;
using CK.MQTT.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public class MqttServerClient : MqttListener, IMqtt3Client, IDisposable
    {
        internal readonly ITopicManager _inputTopicFilter = new SimpleTopicManager(); //TODO: 
        internal readonly ITopicManager _outputTopicFilter = new SimpleTopicManager();
        internal readonly Channel<(Subscription[]?, string[]?)> _subscriptionsCommand = Channel.CreateUnbounded<(Subscription[]?, string[]?)>();
        readonly IMqtt3Sink _sink;
        internal TaskCompletionSource<(IMqttChannel channel, IAuthenticationProtocolHandler securityManager, ILocalPacketStore localPacketStore, IRemotePacketStore remotePacketStore, IConnectInfo connectInfo)>? _needClientTCS;
        ClientWrapper? _wrapper;
        public MqttServerClient( Mqtt3ConfigurationBase config, IMqtt3Sink sink, IMqttChannelFactory channelFactory, IStoreFactory storeFactory, IAuthenticationProtocolHandlerFactory securityManagerFactory ) : base( config, channelFactory, storeFactory )
        {
            SecurityManagerFactory = new SecurityManagerFactoryWrapper( this, securityManagerFactory );
            _sink = sink;
        }

        protected override IAuthenticationProtocolHandlerFactory SecurityManagerFactory { get; }

        protected override ValueTask CreateClientAsync( IActivityMonitor m, IMqttChannel channel, IAuthenticationProtocolHandler securityManager, ILocalPacketStore localPacketStore, IRemotePacketStore remotePacketStore, IConnectInfo connectInfo, CancellationToken cancellationToken )
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
            var (channel, securityManager, localPacketStore, remotePacketStore, connectInfo) = await tcs.Task;
            _wrapper = new ClientWrapper( this, ProtocolConfiguration.FromProtocolLevel( connectInfo.ProtocolLevel ), Config, _sink, channel, remotePacketStore, localPacketStore );
            return new ConnectResult( localPacketStore.IsRevivedSession ? SessionState.SessionPresent : SessionState.CleanSession, ConnectReturnCode.Accepted );
        }

        public async ValueTask<Task> UnsubscribeAsync( params string[] topics )
        {
            await _subscriptionsCommand.Writer.WriteAsync( (null, topics) );
            return Task.CompletedTask;
        }

        public async ValueTask<Task<SubscribeReturnCode[]>> SubscribeAsync( IEnumerable<Subscription> subscriptions )
        {
            var subs = subscriptions.ToArray();
            await _subscriptionsCommand.Writer.WriteAsync( (subs, null) );
            return Task.FromResult(
                new SubscribeReturnCode[subs.Length]
            // We cannot ask the client a certain QoS as it's a fake subscribe, we return 0 because we canno't make the guarentee the QoS will be higher.
            );
        }

        public async ValueTask<Task<SubscribeReturnCode>> SubscribeAsync( Subscription subscriptions )
        {
            await _subscriptionsCommand.Writer.WriteAsync( (new Subscription[] { subscriptions }, null) );
            return Task.FromResult(
                SubscribeReturnCode.MaximumQoS0
            // We cannot ask the client a certain QoS as it's a fake subscribe, we return 0 because we canno't make the guarentee the QoS will be higher.
            );
        }

        public Task<bool> DisconnectAsync( bool deleteSession )
            => _wrapper!.DisconnectAsync( deleteSession );

        public ValueTask<Task> PublishAsync( OutgoingMessage message )
            => _wrapper!.PublishAsync( message );

        public void Dispose()
            => _wrapper?.Dispose();
    }
}
