using CK.MQTT.Client;
using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Server.Server
{
    public class ServerClientMessageSink : MQTTMessageSink, IMQTTServerSink
    {
        public ServerClientMessageSink( ChannelWriter<object?> Events ) : base( Events )
        {
        }

        public record Subscribe( Subscription[] Subscriptions );
        public ValueTask<SubscribeReturnCode[]> OnSubscribeAsync( params Subscription[] subscriptions )
        {
            _events.TryWrite( new Subscribe( subscriptions ) );
            var codes = new SubscribeReturnCode[subscriptions.Length];
            Array.Fill( codes, SubscribeReturnCode.MaximumQoS0 );
            return new( codes );
        }

        public record Unsubscribe( string[] Topics);
        public ValueTask OnUnsubscribeAsync( params string[] topicFilter )
        {
            _events.TryWrite( new Unsubscribe( topicFilter ) );
            return new();
        }
    }
}
