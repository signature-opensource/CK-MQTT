using System.IO.Pipelines;

namespace CK.MQTT.Abstractions.Packets
{
    public class IncomingApplicationMessage
    {
        public IncomingApplicationMessage( string topic, PipeReader pipeReader, bool duplicate, bool retain, int payloadLenght )
        {
            Topic = topic;
            PipeReader = pipeReader;
            Duplicate = duplicate;
            Retain = retain;
            PayloadLenght = payloadLenght;
        }
        public string Topic { get; }

        public PipeReader PipeReader { get; }

        public bool Duplicate { get; }

        public bool Retain { get; }

        public int PayloadLenght { get; }
    }
}
