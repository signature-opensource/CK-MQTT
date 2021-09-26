using System;

namespace CK.MQTT.Client
{
    public class ApplicationMessage
    {
        public ApplicationMessage( string topic, ReadOnlyMemory<byte> payload, QualityOfService qoS, bool retain )
        {
            Topic = topic;
            Payload = payload;
            QoS = qoS;
            Retain = retain;
        }

        public string Topic { get; }
        public ReadOnlyMemory<byte> Payload { get; }
        public QualityOfService QoS { get; }
        public bool Retain { get; }
    }
}
