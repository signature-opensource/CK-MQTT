using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers;

public abstract class LoopBackBase : IMQTTChannel
{
    readonly Action<object?> _writer;

    public LoopBackBase( Action<object?> writer ) => _writer = writer;

    TaskCompletionSource<IDuplexPipe> _tcs = new();
    public Task<IDuplexPipe> GetTestDuplexPipeAsync() => _tcs.Task;
    public abstract IDuplexPipe? DuplexPipe { get; protected set; }

    public bool IsConnected { get; private set; } = false;

    public async ValueTask StartAsync( CancellationToken cancellationToken )
    {
        IsConnected = true;
        _writer( new StartedChannel() );
        _tcs.SetResult( await DoStartAsync( cancellationToken ) );
    }

    protected abstract ValueTask<IDuplexPipe> DoStartAsync( CancellationToken cancellationToken );
    public async ValueTask CloseAsync( DisconnectReason reason )
    {
        if( !IsConnected ) throw new InvalidOperationException( "Closing when not connected." );
        if( DuplexPipe == null ) throw new InvalidOperationException( "Not started." );
        var pipe = await _tcs.Task;
        await pipe!.Input.CompleteAsync();
        await pipe!.Output.CompleteAsync();
        pipe!.Input.CancelPendingRead();
        pipe!.Output.CancelPendingFlush();
        await DuplexPipe.Output.CompleteAsync();
        DuplexPipe.Output.CancelPendingFlush();
        DuplexPipe.Input.CancelPendingRead();
        await DuplexPipe.Input.CompleteAsync();
        DuplexPipe = null;
        IsConnected = false;
        _writer( new ClosedChannel() );

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
