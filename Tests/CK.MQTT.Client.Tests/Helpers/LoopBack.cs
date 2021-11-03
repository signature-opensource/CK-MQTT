using CK.Core;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    public abstract class LoopBack : IMqttChannel
    {
        public abstract IDuplexPipe TestDuplexPipe { get; set; }
        public abstract IDuplexPipe DuplexPipe { get; set; }

        public bool IsConnected { get; private set; } = true;

        public ValueTask StartAsync( IActivityMonitor? m ) => new();
        public void Close( IInputLogger? m )
        {
            TestDuplexPipe.Output.Complete();
            TestDuplexPipe.Output.CancelPendingFlush();
            TestDuplexPipe.Input.CancelPendingRead();
            TestDuplexPipe.Input.Complete();
        }

        public Task OnDisposeTask => _tcs.Task;
        readonly TaskCompletionSource _tcs = new();
        public void Dispose()
        {
            IsConnected = false;
            _tcs.SetResult();
        }
    }
}
