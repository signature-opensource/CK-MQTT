using CK.MQTT.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public interface IMQTTServerSink : IMQTT3Sink
    {
        ValueTask<SubscribeReturnCode[]> OnSubscribeAsync( params Subscription[] subscriptions );
        ValueTask OnUnsubscribeAsync( params string[] topicFilter );
    }
}
