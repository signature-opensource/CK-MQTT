using CK.MQTT.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public class MqttServerSinkWrapper : Mqtt3SinkWrapper, IMqttServerSink
    {
        public MqttServerSinkWrapper( IMqttServerSink sink ) : base( sink )
        {
            ServerSink = sink;
        }

        public IMqttServerSink ServerSink { get; }

        public ValueTask<SubscribeReturnCode[]> OnSubscribeAsync( params Subscription[] subscriptions ) => ServerSink.OnSubscribeAsync( subscriptions );

        public ValueTask OnUnsubscribeAsync( params string[] topicFilter ) => ServerSink.OnUnsubscribeAsync( topicFilter );
    }
}
