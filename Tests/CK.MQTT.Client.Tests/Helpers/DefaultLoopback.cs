using CK.MQTT.Client.Tests.Helpers;
using System.IO.Pipelines;

namespace CK.MQTT.Client.Tests
{
    public class DefaultLoopback : LoopBack
    {
        public DefaultLoopback()
        {
            Pipe input = new();
            Pipe output = new();
            DuplexPipe = new DuplexPipe( input.Reader, output.Writer );
            TestDuplexPipe = new DuplexPipe( output.Reader, input.Writer );
        }

        public override IDuplexPipe DuplexPipe { get; set; }

        public override IDuplexPipe TestDuplexPipe { get; set; }
    }
}
