using CK.MQTT.Client;
using CK.PerfectEvent;

namespace CK.MQTT.Server
{
    public class MQTTServerAgent : MessageExchangerAgent
    {
        public MQTTServerAgent( IConnectedMessageSender sender, MessageWorker messageWorker, string clientId )
            : base(sender, messageWorker )
        {
            messageWorker.Middlewares.Add( new ServerHandler( _subscribeSender, _unsubscribeSender ) );
            ClientId = clientId;
        }
        readonly PerfectEventSender<Subscription> _subscribeSender = new();
        readonly PerfectEventSender<string> _unsubscribeSender = new();

        public PerfectEvent<Subscription> OnSubscribe => _subscribeSender.PerfectEvent;
        public PerfectEvent<string> OnUnsubscribe => _unsubscribeSender.PerfectEvent;

        public string ClientId { get; }
    }
}
