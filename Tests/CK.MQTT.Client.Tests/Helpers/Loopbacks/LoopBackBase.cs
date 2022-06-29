using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    public abstract class LoopBackBase : IMqttChannel
    {
        readonly ChannelWriter<object?> _writer;

        public LoopBackBase( ChannelWriter<object?> writer ) => _writer = writer;

        TaskCompletionSource<IDuplexPipe> _tcs = new();
        public Task<IDuplexPipe> GetTestDuplexPipeAsync() => _tcs.Task;
        public abstract IDuplexPipe? DuplexPipe { get; protected set; }

        public bool IsConnected { get; private set; } = false;

        public async ValueTask StartAsync( CancellationToken cancellationToken )
        {
            IsConnected = true;
            _writer.TryWrite( new StartedChannel() );
            _tcs.SetResult( await DoStartAsync( cancellationToken ) );
        }

        protected abstract ValueTask<IDuplexPipe> DoStartAsync( CancellationToken cancellationToken );
        public void Close()
        {
            if( !IsConnected ) throw new InvalidOperationException( "Closing when not connected." );
            if( DuplexPipe == null ) throw new InvalidOperationException( "Not started." );
#pragma warning disable VSTHRD002 // Test code.
#pragma warning disable VSTHRD104 // Test code.
            var pipe = _tcs.Task.Result;
#pragma warning restore VSTHRD104 // Test code.
#pragma warning restore VSTHRD002 // Test code.
            pipe!.Input.Complete();
            pipe!.Output.Complete();
            pipe!.Input.CancelPendingRead();
            pipe!.Output.CancelPendingFlush();
            DuplexPipe.Output.Complete();
            DuplexPipe.Output.CancelPendingFlush();
            DuplexPipe.Input.CancelPendingRead();
            DuplexPipe.Input.Complete();
            DuplexPipe = null;
            IsConnected = false;
            _writer.TryWrite( new ClosedChannel() );

            _tcs = new();
            DoClose();
        }

        public void CloseConnectionBackdoor()
        {
#pragma warning disable VSTHRD002 // Test code.
#pragma warning disable VSTHRD104 // Test code.
            var pipe = _tcs.Task.Result;
#pragma warning restore VSTHRD104 // Test code.
#pragma warning restore VSTHRD002 // Test code.
            pipe!.Input.Complete();
            pipe!.Output.Complete();
            DuplexPipe!.Output.CancelPendingFlush();
        }
        protected abstract void DoClose();
        public record StartedChannel();
        public record ClosedChannel();
        bool _disposed;
        public void Dispose()
        {
            if( _disposed ) throw new InvalidOperationException( "Double dispose" );
            _disposed = true;
        }
    }
}
