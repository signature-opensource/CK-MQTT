using System.IO.Pipelines;

namespace CK.MQTT
{
    public class IncomingMessage
    {
        public IncomingMessage( string topic, PipeReader pipeReader, bool duplicate, bool retain, int payloadLength )
        {
            Topic = topic;
            PipeReader = pipeReader;
            Duplicate = duplicate;
            Retain = retain;
            PayloadLength = payloadLength;
        }
        public string Topic { get; }

        public PipeReader PipeReader { get; }

        public bool Duplicate { get; }

        public bool Retain { get; }

        public int PayloadLength { get; }
    }
}
