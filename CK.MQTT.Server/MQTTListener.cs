using CK.Core;
using CK.MQTT.Stores;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server;

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
        await _cts.CancelAsync();
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
            (IMQTTChannel? channel, var connectionInfo) = await _channelFactory.CreateAsync( cancellationToken );
            var token = m.CreateToken();
            _ = Task.Run( () => AcceptSingleClientAsync( token, channel, connectionInfo, cancellationToken ), cancellationToken );
        }
    }

    async Task AcceptSingleClientAsync( ActivityMonitor.Token token, IMQTTChannel channelIn, string connectionInfo,
        CancellationToken cancellationToken )
    {
        var m = new ActivityMonitor();
        using var _ = m.StartDependentActivity( token );
        IAuthenticationProtocolHandler? securityManager = null;
        IMQTTChannel? channel = channelIn;
        try
        {
            //channel is disposable, this accept loop should not crash.
            //if it does, the server is screwed, and this is a bug

            if( cancellationToken.IsCancellationRequested ) return;

            securityManager =
                await AuthProtocolHandlerFactory.ChallengeIncomingConnectionAsync( connectionInfo,
                    cancellationToken );
            if( securityManager is null ) return;

            if( cancellationToken.IsCancellationRequested ) return;
            await channel.StartAsync( cancellationToken );
            if( cancellationToken.IsCancellationRequested ) return;
            var connectHandler = new ConnectHandler();
            (ProtocolConnectReturnCode returnCode, ProtocolLevel protocolLevel) =
                await connectHandler.HandleAsync( channel.DuplexPipe.Input, securityManager, cancellationToken );
            if( cancellationToken.IsCancellationRequested ) return;
            if( returnCode != ProtocolConnectReturnCode.Accepted )
            {
                if( returnCode !=
                    ProtocolConnectReturnCode
                        .Unknown ) // Unknown malformed data or internal error. In this case we don't answer and close the connection.
                {
                    await new OutgoingConnectAck( false, returnCode ).WriteAsync( protocolLevel,
                        channel.DuplexPipe.Output, cancellationToken );
                }
                return;
            }

            (ILocalPacketStore localStore, IRemotePacketStore remoteStore) = await _storeFactory.CreateAsync(
                ProtocolConfiguration.FromProtocolLevel( protocolLevel ), Config, connectHandler.ClientId,
                connectHandler.CleanSession, cancellationToken );
            // ownership of channel and security manager is now transferred to the CreateClientAsync implementation.
            var channelCopy =
                channel; // We copy the reference so these objects are not cleaned if the cancellationToken is triggered.
            var smCopy = securityManager;
            channel = null;
            securityManager = null;
            await CreateClientAsync( m, connectHandler.ClientId, channelCopy, smCopy, localStore, remoteStore,
               connectHandler, cancellationToken );
            await new OutgoingConnectAck( false, returnCode ).WriteAsync( protocolLevel, channelCopy.DuplexPipe.Output,
                cancellationToken );
            await channelCopy.DuplexPipe.Output.FlushAsync( cancellationToken );
            if( cancellationToken.IsCancellationRequested )
            {
                localStore.Dispose();
                remoteStore.Dispose();
                return;
            }
        }
        finally
        {
            if( securityManager is not null )
            {
                await securityManager.DisposeAsync();
            }

            if( channel is not null )
            {
                await channel.CloseAsync( DisconnectReason.RemoteDisconnected );
                channel.Dispose();
            }
        }
    }

    public virtual void Dispose()
    {
        _channelFactory.Dispose();
        _cts?.Cancel();
    }
}
