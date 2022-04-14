using CK.MQTT.Client.Tests.Helpers;
using System;
using System.IO.Pipelines;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests
{
    public class DefaultLoopback : LoopBackBase
    {
        public DefaultLoopback( ChannelWriter<object?> writer ) : base(writer)
        {
        }

        public override ValueTask StartAsync()
        {
            Pipe input = new( new PipeOptions( pauseWriterThreshold: long.MaxValue ) );
            Pipe output = new( new PipeOptions( pauseWriterThreshold: long.MaxValue ) );
            DuplexPipe = new DuplexPipe( input.Reader, output.Writer );
            TestDuplexPipe = new DuplexPipe( output.Reader, input.Writer );
            return new ValueTask();
        }

        public override IDuplexPipe? DuplexPipe { get; protected set; }
        
        public override IDuplexPipe? TestDuplexPipe { get; protected set; }

        public override void Close()
        {
            if( DuplexPipe == null ) throw new InvalidOperationException( "Not started." );
            DuplexPipe.Output.Complete();
            DuplexPipe.Output.CancelPendingFlush();
            DuplexPipe.Input.CancelPendingRead();
            DuplexPipe.Input.Complete();
            DuplexPipe = null;
        }
    }
}
