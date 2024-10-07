using CK.MQTT.Client;
using System.Threading.Tasks;

namespace CK.MQTT.Server;

public interface IMQTTServerSink : IMQTT3Sink
{
    ValueTask<SubscribeReturnCode[]> OnSubscribeAsync( params Subscription[] subscriptions );
    ValueTask OnUnsubscribeAsync( params string[] topicFilter );
}
