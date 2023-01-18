using CK.Core;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Middleware
{
    class HandleSynchronize : IAgentMessageMiddleware
    {
        

        public ValueTask<bool> HandleAsync( IActivityMonitor m, object? message )
        {
            if( message is SynchronizationMessage synchronizationMessage )
            {
                synchronizationMessage.Tcs.SetResult();
                return new ValueTask<bool>( true );
            }
            return new ValueTask<bool>( false );
        }

        public ValueTask DisposeAsync() => new ValueTask();
    }
}
