using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.Server.Server;
using CK.PerfectEvent;
using System;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public class MQTTServerAgent : MessageExchangerAgent
    {
        readonly ServerClientMessageSink _sink = new();
        public MQTTServerAgent( string clientId, Func<IMQTTServerSink, IConnectedMessageSender> factory)
        {
            Start();
            factory( _sink ); //TODO: big code smell. We don't use the output there.
            ClientId = clientId;
        }
        readonly PerfectEventSender<Subscription> _subscribeSender = new();
        readonly PerfectEventSender<string> _unsubscribeSender = new();
        protected override MQTTMessageSink MessageSink => _sink;

        public PerfectEvent<Subscription> OnSubscribe => _subscribeSender.PerfectEvent;
        public PerfectEvent<string> OnUnsubscribe => _unsubscribeSender.PerfectEvent;

        public string ClientId { get; }

        protected override async Task ProcessMessageAsync( IActivityMonitor m, object? item )
        {
            switch( item )
            {
                case ServerClientMessageSink.Subscribe subscribe:
                    foreach( var sub in subscribe.Subscriptions )
                    {
                        await _subscribeSender.SafeRaiseAsync( m, sub );
                    }
                    return;

                case ServerClientMessageSink.Unsubscribe unsubscribe:
                    foreach( var topic in unsubscribe.Topics )
                    {
                        await _unsubscribeSender.SafeRaiseAsync( m, topic );
                    }
                    return;
                default:
                    await base.ProcessMessageAsync( m, item );
                    break;
            }
        }
    }
}
