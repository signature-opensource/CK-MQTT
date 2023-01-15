using CK.MQTT.Client.Tests.Helpers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests
{
    public class DefaultLoopback : LoopBackBase
    {
        public DefaultLoopback( ChannelWriter<object?> writer ) : base( writer )
        {
        }

        protected override ValueTask<IDuplexPipe> DoStartAsync( CancellationToken cancellationToken )
        {
            Pipe input = new( new PipeOptions( pauseWriterThreshold: long.MaxValue ) );
            Pipe output = new( new PipeOptions( pauseWriterThreshold: long.MaxValue ) );
            DuplexPipe = new DuplexPipe( input.Reader, output.Writer );
            return new ValueTask<IDuplexPipe>( new DuplexPipe( output.Reader, input.Writer ) );
        }

        public override IDuplexPipe? DuplexPipe { get; protected set; }


        protected override void DoClose()
        {
           
        }
    }
}
