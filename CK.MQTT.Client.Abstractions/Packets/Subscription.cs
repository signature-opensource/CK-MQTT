using System;

namespace CK.MQTT.Common.Packets
{
    public class Subscription
    {
        public Subscription( string topicFilter, QualityOfService requestedQos )
        {
            TopicFilter = topicFilter;
            MaximumQualityOfService = requestedQos;
        }

        public string TopicFilter { get; set; }

        public QualityOfService MaximumQualityOfService { get; set; }

    }
}
