using CK.Core;
using CK.MQTT.Server.Server;
using CK.MQTT.Server.ServerClient;
using CK.MQTT.Stores;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public class DefaultMQTTDemiServer : MqttDemiServer, IHostedService, ISingletonAutoService, IDisposable
    {
        public DefaultMQTTDemiServer( IOptionsMonitor<MQTTDemiServerConfig> config )
            : base(
                  new DynamicallyConfiguredConfig( config ),
                  new DynamicallyConfiguredChannelFactory( config ),
                  new MemoryStoreFactory(),
                  new DynamicallyConfiguredAuthenticationProtocolHandlerFactory( config )
        )
        {
        }

        class DynamicallyConfiguredConfig : Mqtt3ConfigurationBase, IDisposable
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
}
