using System;
using System.IO.Pipelines;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    public abstract class LoopBack : IMqttChannel
    {
        readonly ChannelWriter<object?> _writer;

        public LoopBack( ChannelWriter<object?> writer )
        {
            _writer = writer;
        }
        public abstract IDuplexPipe TestDuplexPipe { get; set; }
        public abstract IDuplexPipe DuplexPipe { get; set; }

        public bool IsConnected { get; private set; } = true;

        public ValueTask StartAsync() => new();
        public abstract void Close();

        public record DisposedChannel();
        public void Dispose()
        {
            if( !IsConnected ) throw new InvalidOperationException( "Double dispose" );
            IsConnected = false;
            _writer.TryWrite( new DisposedChannel() );
        }
    }
}
