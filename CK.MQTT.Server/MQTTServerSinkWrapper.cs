using CK.MQTT.Client;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public class MQTTServerSinkWrapper : MQTT3SinkWrapper, IMQTTServerSink
    {
        public MQTTServerSinkWrapper( IMQTTServerSink sink ) : base( sink )
        {
            ServerSink = sink;
        }

        public IMQTTServerSink ServerSink { get; }

        public ValueTask<SubscribeReturnCode[]> OnSubscribeAsync( params Subscription[] subscriptions ) => ServerSink.OnSubscribeAsync( subscriptions );

        public ValueTask OnUnsubscribeAsync( params string[] topicFilter ) => ServerSink.OnUnsubscribeAsync( topicFilter );
    }
}
