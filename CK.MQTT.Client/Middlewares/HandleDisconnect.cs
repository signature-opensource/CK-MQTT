using CK.Core;
using CK.PerfectEvent;
using System.Threading.Tasks;
using static CK.MQTT.Client.MQTTMessageSink;

namespace CK.MQTT.Client.Middleware;

class HandleDisconnect : IAgentMessageMiddleware
{
    readonly PerfectEventSender<DisconnectReason> _onConnectionChangeSender;

    public HandleDisconnect( PerfectEventSender<DisconnectReason> onConnectionChangeSender )
    {
        _onConnectionChangeSender = onConnectionChangeSender;
    }

    public ValueTask DisposeAsync() => new ValueTask();

    public async ValueTask<bool> HandleAsync( IActivityMonitor m, object? message )
    {
        if( message is not UnattendedDisconnect disconnect ) return false;
        await _onConnectionChangeSender.RaiseAsync( m, disconnect.Reason );
        return true;
    }
}
