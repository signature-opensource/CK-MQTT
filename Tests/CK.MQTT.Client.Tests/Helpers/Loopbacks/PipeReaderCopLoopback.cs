using CK.MQTT.Client.Tests.Helpers;
using System;
using System.IO.Pipelines;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests
{
    public class PipeReaderCopLoopback : LoopBackBase
    {
        public PipeReaderCopLoopback( ChannelWriter<object?> writer ) : base( writer )
        {
        }
        
        public override ValueTask StartAsync()
        {
            Pipe input = new( new PipeOptions( pauseWriterThreshold: long.MaxValue ) );
            Pipe output = new( new PipeOptions( pauseWriterThreshold: long.MaxValue ) );
            DuplexPipe = new DuplexPipe( new PipeReaderCop( input.Reader ), output.Writer );
            TestDuplexPipe = new DuplexPipe( new PipeReaderCop( output.Reader ), input.Writer );
            return new ValueTask();
        }
        
        public override IDuplexPipe? DuplexPipe { get; protected set; }
        
        public override IDuplexPipe? TestDuplexPipe { get; protected set; }

        public override void Close()
        {
            if( TestDuplexPipe == null ) throw new InvalidOperationException( "Not started." );
            TestDuplexPipe.Output.Complete();
            TestDuplexPipe.Output.CancelPendingFlush();
            TestDuplexPipe.Input.CancelPendingRead();
            TestDuplexPipe.Input.Complete();
        }
    }
}
