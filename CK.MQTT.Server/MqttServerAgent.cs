using CK.Core;
using CK.MQTT.Client;
using CK.MQTT.Client.ExtensionMethods;
using CK.MQTT.Server.Server;
using CK.PerfectEvent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public class MqttServerAgent : MessageExchangerAgent
    {
        readonly ServerClientMessageSink _sink = new();
        public MqttServerAgent(Func<IMqttServerSink, IConnectedMessageSender> factory)
        {
            Start();
            factory( _sink ); //TODO: big code smell. We don't use the output there.
        }
        readonly PerfectEventSender<Subscription> _subscribeSender = new();
        readonly PerfectEventSender<string> _unsubscribeSender = new();
        protected override MqttMessageSink MessageSink => _sink;

        public PerfectEvent<Subscription> OnSubscribe => _subscribeSender.PerfectEvent;
        public PerfectEvent<string> OnUnsubscribe => _unsubscribeSender.PerfectEvent;

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
