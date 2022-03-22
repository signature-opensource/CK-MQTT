using CK.Core;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    [ExcludeFromCodeCoverage]
    public abstract class LoopBack : IMqttChannel
    {
        public abstract IDuplexPipe TestDuplexPipe { get; set; }
        public abstract IDuplexPipe DuplexPipe { get; set; }

        public bool IsConnected { get; private set; } = true;

        public ValueTask StartAsync( ) => new();
        public abstract void Close();

        public Task OnDisposeTask => _tcs.Task;
        readonly TaskCompletionSource _tcs = new();
        public void Dispose()
        {
            if( !IsConnected ) throw new InvalidOperationException( "Double dispose" );
            IsConnected = false;
            _tcs.SetResult();
        }
    }
}
