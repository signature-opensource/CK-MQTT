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

        public override bool Equals( object? obj )
        {
            return obj is ApplicationMessage message &&
                   Topic == message.Topic &&
                   Payload.Span.SequenceEqual( message.Payload.Span ) &&
                   QoS == message.QoS &&
                   Retain == message.Retain;
        }

        public override int GetHashCode() => HashCode.Combine( Topic, Payload, QoS, Retain );
    }
}
