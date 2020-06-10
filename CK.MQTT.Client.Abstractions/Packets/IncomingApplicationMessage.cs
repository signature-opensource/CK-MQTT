using System.IO.Pipelines;

namespace CK.MQTT.Abstractions.Packets
{
    public class IncomingApplicationMessage
    {
        public IncomingApplicationMessage( string topic, PipeReader pipeReader )
        {
            Topic = topic;
            PipeReader = pipeReader;
        }
        public string Topic { get; }

        public PipeReader PipeReader { get; }
    }
}
