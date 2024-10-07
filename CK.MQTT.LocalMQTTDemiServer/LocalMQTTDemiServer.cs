using CK.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server;

public class LocalMQTTDemiServer : MQTTDemiServer, IHostedService, ISingletonAutoService, IDisposable
{
    public LocalMQTTDemiServer( IOptionsMonitor<MQTTDemiServerConfig> config )
        : base(
              new DynamicallyConfiguredConfig( config ),
              new DynamicallyConfiguredChannelFactory( config ),
              new MemoryStoreFactory(),
              new DynamicallyConfiguredAuthenticationProtocolHandlerFactory( config )
    )
    {
    }

    class DynamicallyConfiguredConfig : MQTT3ConfigurationBase, IDisposable
    {
        readonly IDisposable _disposable;
        public DynamicallyConfiguredConfig( IOptionsMonitor<MQTTDemiServerConfig> config )
        {
            _disposable = config.OnChange( ApplyConfig );
        }

        public void Dispose()
        {
            _disposable.Dispose();
        }

        void ApplyConfig( MQTTDemiServerConfig config )
        {
            AttemptCountBeforeGivingUpPacket = config.ImplementationConfig.AttemptCountBeforeGivingUpPacket;
            IdStoreStartCount = config.ImplementationConfig.IdStoreStartCount;
            OutgoingPacketsChannelCapacity = config.ImplementationConfig.OutgoingPacketsChannelCapacity;
            StoreFullWaitTimeoutMs = config.ImplementationConfig.StoreFullWaitTimeoutMs;
            WaitTimeoutMilliseconds = config.ImplementationConfig.WaitTimeoutMilliseconds;
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        ((IDisposable)Config).Dispose();
    }

    public Task StartAsync( CancellationToken cancellationToken )
    {
        StartListening();
        return Task.CompletedTask;
    }

    public async Task StopAsync( CancellationToken cancellationToken )
    {
        await StopListeningAsync();
    }
}
