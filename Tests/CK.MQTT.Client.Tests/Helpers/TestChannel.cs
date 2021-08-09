using CK.Core;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests
{
    class TestChannel : IMqttChannel
    {
        public TestChannel()
        {
            Pipe input = new();
            Pipe output = new();
            DuplexPipe = new DuplexPipe( input.Reader, output.Writer );
            TestDuplexPipe = new DuplexPipe( output.Reader, input.Writer );
        }

        public bool IsConnected { get; private set; } = true;

        public IDuplexPipe DuplexPipe { get; }

        public IDuplexPipe TestDuplexPipe { get; set; }

        public void Close( IInputLogger? m ) { }

        public void Dispose() { }

        public ValueTask StartAsync( IActivityMonitor? m ) => new ValueTask();
    }
}
