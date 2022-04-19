using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.P2P;
using CK.MQTT.Packets;
using CK.MQTT.Stores;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public abstract class MqttListener
    {
        Task? _acceptLoop;
        CancellationTokenSource? _cts;
        readonly IMqttChannelFactory _channelFactory;
        readonly IStoreFactory _storeFactory;

        protected Mqtt3ConfigurationBase Config { get; }

        public MqttListener( Mqtt3ConfigurationBase config, IMqttChannelFactory channelFactory, IStoreFactory storeFactory )
        {
            Config = config;
            _channelFactory = channelFactory;
            _storeFactory = storeFactory;
        }

        protected abstract IAuthenticationProtocolHandlerFactory SecurityManagerFactory { get; }

        public void StartListening()
        {
            var cts = new CancellationTokenSource();
            _cts = cts;
            _acceptLoop = AcceptLoopAsync( cts.Token );
        }

        public async ValueTask StopListeningAsync()
        {
            if( _cts == null || _acceptLoop == null ) throw new InvalidOperationException( "Not running." );
            _cts.Cancel();
            await _acceptLoop;
            _cts = null;
            _acceptLoop = null;
        }

        protected abstract ValueTask CreateClientAsync(
            IActivityMonitor m,
            IMqttChannel channel,
            IAuthenticationProtocolHandler securityManager,
            ILocalPacketStore localPacketStore,
            IRemotePacketStore remotePacketStore,
            IConnectInfo connectInfo,
            CancellationToken cancellationToken );

        async Task AcceptLoopAsync( CancellationToken cancellationToken )
        {
            var m = new ActivityMonitor();
            while( !cancellationToken.IsCancellationRequested )
            {
                IAuthenticationProtocolHandler? securityManager = null;
                (IMqttChannel? channel, var connectionInfo) = await _channelFactory.CreateAsync( cancellationToken );
                try
                {
                    void CloseConnection()
                    {
                        channel.Close();
                        channel.Dispose();
                        channel = null;
                    }

                    //channel is disposable, this accept loop should not crash.
                    //if it does, the server is screwed, and this is a bug (and I doesn't care that the disposable leak).

                    if( cancellationToken.IsCancellationRequested ) return;

                    securityManager = await SecurityManagerFactory.ChallengeIncomingConnectionAsync( connectionInfo, cancellationToken );
                    if( securityManager is null )
                    {
                        CloseConnection();
                        continue;
                    }
                    if( cancellationToken.IsCancellationRequested ) return;
                    await channel.StartAsync( cancellationToken );
                    if( cancellationToken.IsCancellationRequested ) return;
                    var connectHandler = new ConnectHandler();
                    (ConnectReturnCode returnCode, ProtocolLevel protocolLevel) = await connectHandler.HandleAsync( channel.DuplexPipe.Input, securityManager, cancellationToken );
                    if( cancellationToken.IsCancellationRequested ) return;
                    if( returnCode != ConnectReturnCode.Accepted )
                    {
                        if( returnCode != ConnectReturnCode.Unknown ) // Unknown malformed data or internal error. In this case we don't answer and close the connection.
                        {
                            await new OutgoingConnectAck( false, returnCode ).WriteAsync( protocolLevel, channel.DuplexPipe.Output, cancellationToken );
                        }
                        CloseConnection();
                        continue;
                    }

                    (ILocalPacketStore localStore, IRemotePacketStore remoteStore) = await _storeFactory.CreateAsync( ProtocolConfiguration.FromProtocolLevel( protocolLevel ), Config, connectHandler.ClientId, connectHandler.CleanSession, cancellationToken );
                    if( cancellationToken.IsCancellationRequested )
                    {
                        localStore.Dispose();
                        remoteStore.Dispose();
                        return;
                    }
                    // ownership of channel and security manager is now transfered to the CreateClientAsync implementation.
                    var channelCopy = channel; // We copy the reference so these objects are not cleaned if the cancellationToken is triggered.
                    var smCopy = securityManager;
                    channel = null;
                    securityManager = null;
                    await CreateClientAsync( m, channelCopy, smCopy, localStore, remoteStore, connectHandler, cancellationToken );
                }
                finally
                {
                    if( securityManager is not null )
                    {
                        await securityManager.DisposeAsync();
                    }
                    channel?.Close();
                    channel?.Dispose();
                }
            }
        }
    }
}
