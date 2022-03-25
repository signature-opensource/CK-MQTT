using CK.MQTT.Client.Tests.Helpers;
using System.IO.Pipelines;
using System.Threading.Channels;

namespace CK.MQTT.Client.Tests
{
    public class PipeReaderCopLoopback : LoopBack
    {
        public PipeReaderCopLoopback( ChannelWriter<object?> writer ) : base( writer )
        {
            Pipe input = new();
            Pipe output = new();
            DuplexPipe = new DuplexPipe( new PipeReaderCop( input.Reader ), output.Writer );
            TestDuplexPipe = new DuplexPipe( new PipeReaderCop( output.Reader ), input.Writer );
        }

        public override IDuplexPipe DuplexPipe { get; set; }

        public override IDuplexPipe TestDuplexPipe { get; set; }

        public override void Close()
        {
            TestDuplexPipe.Output.Complete();
            TestDuplexPipe.Output.CancelPendingFlush();
            TestDuplexPipe.Input.CancelPendingRead();
            TestDuplexPipe.Input.Complete();
        }
    }
}
