using CK.Core;
using CK.PerfectEvent;
using System.Threading.Tasks;
using static CK.MQTT.Client.DefaultClientMessageSink;

namespace CK.MQTT.Client.Middleware;

class HandleConnected : IAgentMessageMiddleware
{
    readonly PerfectEventSender<DisconnectReason> _onConnectionChangeSender;

    public HandleConnected( PerfectEventSender<DisconnectReason> onConnectionChangeSender )
        => _onConnectionChangeSender = onConnectionChangeSender;

    public ValueTask DisposeAsync() => new ValueTask();

    public async ValueTask<bool> HandleAsync( IActivityMonitor m, object? message )
    {
        if( message is Connected )
        {
            await _onConnectionChangeSender.RaiseAsync( m, DisconnectReason.None );
            return true;
        }
        return false;
    }
}
