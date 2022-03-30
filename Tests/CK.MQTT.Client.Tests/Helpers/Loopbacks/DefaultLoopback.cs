using CK.MQTT.Client.Tests.Helpers;
using System.IO.Pipelines;
using System.Threading.Channels;

namespace CK.MQTT.Client.Tests
{
    public class DefaultLoopback : LoopBack
    {
        public DefaultLoopback( ChannelWriter<object?> writer ) : base(writer)
        {
            Pipe input = new( new PipeOptions( pauseWriterThreshold: long.MaxValue ) );
            Pipe output = new( new PipeOptions( pauseWriterThreshold: long.MaxValue ) );
            DuplexPipe = new DuplexPipe( input.Reader, output.Writer );
            TestDuplexPipe = new DuplexPipe( output.Reader, input.Writer );
        }

        public override IDuplexPipe DuplexPipe { get; set; }

        public override IDuplexPipe TestDuplexPipe { get; set; }

        public override void Close()
        {
            DuplexPipe.Output.Complete();
            DuplexPipe.Output.CancelPendingFlush();
            DuplexPipe.Input.CancelPendingRead();
            DuplexPipe.Input.Complete();
        }
    }
}
