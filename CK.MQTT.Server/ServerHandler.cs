using CK.Core;
using CK.MQTT.Client.Middleware;
using CK.MQTT.Server.Server;
using CK.PerfectEvent;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    class ServerHandler : IAgentMessageMiddleware
    {
        readonly PerfectEventSender<Subscription> _subscribeSender = new();
        readonly PerfectEventSender<string> _unsubscribeSender = new();

        public ServerHandler( PerfectEventSender<Subscription> subscribeSender, PerfectEventSender<string> unsubscribeSender )
        {
            _subscribeSender = subscribeSender;
            _unsubscribeSender = unsubscribeSender;
        }

        public ValueTask DisposeAsync() => new ValueTask();

        public async ValueTask<bool> HandleAsync( IActivityMonitor m, object? message )
        {
            switch( message )
            {
                case ServerClientMessageSink.Subscribe subscribe:
                    foreach( var sub in subscribe.Subscriptions )
                    {
                        await _subscribeSender.SafeRaiseAsync( m, sub );
                    }
                    return true;

                case ServerClientMessageSink.Unsubscribe unsubscribe:
                    foreach( var topic in unsubscribe.Topics )
                    {
                        await _unsubscribeSender.SafeRaiseAsync( m, topic );
                    }
                    return true;
                default:
                    return false;
            }
        }
    }
}
