using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Middleware
{
    public interface IAgentMessageMiddleware : IAsyncDisposable
    {
        /// <returns><see langword="true"/> if the message has been handled, <see langword="false"/> otherwise, the next middleware will process it.</returns>
        public ValueTask<bool> HandleAsync( IActivityMonitor m, object? message );
    }
}
