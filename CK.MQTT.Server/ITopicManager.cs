using System.Threading.Tasks;

namespace CK.MQTT.P2P
{
    public interface ITopicManager : ITopicFilter
    {
        public ValueTask<SubscribeReturnCode[]> SubscribeAsync( params Subscription[] subscriptions );
        public ValueTask UnsubscribeAsync( params string[] topicFilter );
        public ValueTask ResetAsync();
    }
}
