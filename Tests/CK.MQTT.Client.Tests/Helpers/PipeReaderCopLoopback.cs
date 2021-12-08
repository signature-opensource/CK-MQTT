using CK.MQTT.Client.Tests.Helpers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;

namespace CK.MQTT.Client.Tests
{
    [ExcludeFromCodeCoverage]
    public class PipeReaderCopLoopback : LoopBack
    {
        public PipeReaderCopLoopback()
        {
            Pipe input = new();
            Pipe output = new();
            DuplexPipe = new DuplexPipe( new PipeReaderCop( input.Reader ), output.Writer );
            TestDuplexPipe = new DuplexPipe( new PipeReaderCop( output.Reader ), input.Writer );
        }

        public override IDuplexPipe DuplexPipe { get; set; }

        public override IDuplexPipe TestDuplexPipe { get; set; }

        public override void Close( IInputLogger? m )
        {
            TestDuplexPipe.Output.Complete();
            TestDuplexPipe.Output.CancelPendingFlush();
            TestDuplexPipe.Input.CancelPendingRead();
            TestDuplexPipe.Input.Complete();
        }
    }
}
