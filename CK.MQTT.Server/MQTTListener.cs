using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.Packets;
using CK.MQTT.Stores;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public abstract class MQTTListenerBase : IDisposable
    {
        Task? _acceptLoop;
        CancellationTokenSource? _cts;
        readonly IMQTTChannelFactory _channelFactory;
        readonly IStoreFactory _storeFactory;

        protected MQTT3ConfigurationBase Config { get; }

        public MQTTListenerBase(
            MQTT3ConfigurationBase config,
            IMQTTChannelFactory channelFactory,
            IStoreFactory storeFactory,
            IAuthenticationProtocolHandlerFactory authenticationProtocolHandler
        )
        {
            Config = config;
            _channelFactory = channelFactory;
            _storeFactory = storeFactory;
            AuthProtocolHandlerFactory = authenticationProtocolHandler;
        }

        protected IAuthenticationProtocolHandlerFactory AuthProtocolHandlerFactory { get; set; }

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
            string clientId,
            IMQTTChannel channel,
            IAuthenticationProtocolHandler securityManager,
            ILocalPacketStore localPacketStore,
            IRemotePacketStore remotePacketStore,
            IConnectInfo connectInfo, CancellationToken cancellationToken );

        async Task AcceptLoopAsync( CancellationToken cancellationToken )
        {
            var m = new ActivityMonitor();
            while( !cancellationToken.IsCancellationRequested )
            {
                IAuthenticationProtocolHandler? securityManager = null;
                (IMQTTChannel? channel, var connectionInfo) = await _channelFactory.CreateAsync( cancellationToken );
                try
                {
                    async ValueTask CloseConnectionAsync()
                    {
                        await channel.CloseAsync( DisconnectReason.None );
                        channel.Dispose();
                        channel = null;
                    }

                    //channel is disposable, this accept loop should not crash.
                    //if it does, the server is screwed, and this is a bug

                    if( cancellationToken.IsCancellationRequested ) return;

                    securityManager = await AuthProtocolHandlerFactory.ChallengeIncomingConnectionAsync( connectionInfo, cancellationToken );
                    if( securityManager is null )
                    {
                        await CloseConnectionAsync();
                        continue;
                    }
                    if( cancellationToken.IsCancellationRequested ) return;
                    await channel.StartAsync( cancellationToken );
                    if( cancellationToken.IsCancellationRequested ) return;
                    var connectHandler = new ConnectHandler();
                    (ProtocolConnectReturnCode returnCode, ProtocolLevel protocolLevel) = await connectHandler.HandleAsync( channel.DuplexPipe.Input, securityManager, cancellationToken );
                    if( cancellationToken.IsCancellationRequested ) return;
                    if( returnCode != ProtocolConnectReturnCode.Accepted )
                    {
                        if( returnCode != ProtocolConnectReturnCode.Unknown ) // Unknown malformed data or internal error. In this case we don't answer and close the connection.
                        {
                            await new OutgoingConnectAck( false, returnCode ).WriteAsync( protocolLevel, channel.DuplexPipe.Output, cancellationToken );
                        }
                        await CloseConnectionAsync();
                        continue;
                    }

                    (ILocalPacketStore localStore, IRemotePacketStore remoteStore) = await _storeFactory.CreateAsync( ProtocolConfiguration.FromProtocolLevel( protocolLevel ), Config, connectHandler.ClientId, connectHandler.CleanSession, cancellationToken );
                    await new OutgoingConnectAck( false, returnCode ).WriteAsync( protocolLevel, channel.DuplexPipe.Output, cancellationToken );
                    await channel.DuplexPipe.Output.FlushAsync( cancellationToken );
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
                    await CreateClientAsync( m, connectHandler.ClientId, channelCopy, smCopy, localStore, remoteStore, connectHandler, cancellationToken );
                }
                finally
                {
                    if( securityManager is not null )
                    {
                        await securityManager.DisposeAsync();
                    }
                    if( channel != null )
                    {
                        await channel.CloseAsync( DisconnectReason.None ); ;
                        channel.Dispose();
                    }
                }
            }
        }

        public virtual void Dispose()
        {
            _channelFactory.Dispose();
            _cts?.Cancel();
        }
    }
}