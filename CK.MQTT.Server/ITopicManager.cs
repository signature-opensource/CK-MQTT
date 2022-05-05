using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public interface ITopicManager : ITopicFilter
    {
        public ValueTask<SubscribeReturnCode[]> SubscribeAsync( params Subscription[] subscriptions );
        public ValueTask UnsubscribeAsync( params string[] topicFilter );
        public ValueTask ResetAsync();
    }
}
