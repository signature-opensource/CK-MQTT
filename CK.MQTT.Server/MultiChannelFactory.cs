using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server;

public class MultiChannelFactory : IMQTTChannelFactory
{
    readonly List<IMQTTChannelFactory> _factories;
    readonly List<(Task<(IMQTTChannel channel, string connectionInfo)> task, CancellationTokenSource cts)?> _createAsync;
    readonly SemaphoreSlim _channelListLock = new( 1 );
    TaskCompletionSource _newChannelTCS = new();
    public MultiChannelFactory()
    {
        _factories = new();
        _createAsync = new();
    }
    public async ValueTask<(IMQTTChannel channel, string connectionInfo)> CreateAsync( CancellationToken cancellationToken )
    {
        while( !cancellationToken.IsCancellationRequested )
        {
            await _channelListLock.WaitAsync( cancellationToken );
            IEnumerable<Task> tasks;
            try
            {
                // We don't want to capture a cancellation token, and it be cancelled after we successfully returned a channel.
                // We proxy the cancellationToken to a cts, which will be decoupled when the channels are created.
                var res = PullCompletedChannel();
                if( res.Item1 != null ) return res; // A completed channel was present.
                for( int i = 0; i < _createAsync.Count; i++ ) // Refill channel that are currently not listening.
                {
                    if( !_createAsync[i].HasValue )
                    {
                        var cts = new CancellationTokenSource(); //crashing is not an option, we are fine not using using.
                        var newCurr = _factories[i].CreateAsync( cts.Token );
                        if( newCurr.IsCompleted )
                        {
                            cts.Dispose();
                            // if it complete instantly we don't need to change the state.
                            return await newCurr;
                        }
                        _createAsync[i] = (newCurr.AsTask(), cts);
                    }
                }
                tasks = _createAsync.Select( s => (Task)(s!.Value.task) ).Append( _newChannelTCS.Task ); // allow to stop waiting when a channel is added.
            }
            finally
            {
                _channelListLock.Release();
            }

            // We don't lock when waiting a channel to be created.
            await Task.WhenAny( tasks );

            await _channelListLock.WaitAsync( cancellationToken );
            try
            {
                if( _newChannelTCS.Task.IsCompleted )  // reload the trigger
                {
                    _newChannelTCS = new();
                }
                var res2 = PullCompletedChannel();
                if( res2.Item1 != null ) return res2;
            }
            finally
            {
                _channelListLock.Release();
            }

            // We will hit this codepath when a channel is being destroyed, or a channel was added.
            // The creation task is cancelled, but we want this to keep running.
        }
        throw new OperationCanceledException();

        (IMQTTChannel, string) PullCompletedChannel()
        {
            for( int i = 0; i < _createAsync.Count; i++ )
            {
                var curr = _createAsync[i];
                if( !curr.HasValue ) continue;
                if( curr.Value.task.IsCanceled ) continue;
                if( curr.Value.task.IsFaulted ) throw curr.Value.task.Exception!;
                if( curr.Value.task.IsCompleted )
                {
#pragma warning disable VSTHRD103 // Task is known to be completed.
                    var res = curr.Value.task.Result;
#pragma warning restore VSTHRD103 //
                    curr.Value.cts.Dispose();
                    _createAsync[i] = default;
                    return res;
                }
            }
            return default;
        }
    }

    public async Task AddFactoryAsync( IMQTTChannelFactory factory )
    {
        await _channelListLock.WaitAsync();
        try
        {
            _factories.Add( factory );
            _createAsync.Add( null );
        }
        finally
        {
            _channelListLock.Release();
        }
    }

    public async Task<bool> RemoveFactoryAsync( IActivityMonitor m, IMQTTChannelFactory factory )
    {
        await _channelListLock.WaitAsync();
        try
        {
            return await DoRemoveFactoryAsync( m, factory );
        }
        finally
        {
            _channelListLock.Release();
        }
    }

    public async Task RemoveAllFactoriesAsync( IActivityMonitor m )
    {
        await _channelListLock.WaitAsync();
        try
        {
            while( _factories.Count > 0 )
            {
                await DoRemoveFactoryAsync( m, _factories[0] );
            }
        }
        finally
        {
            _channelListLock.Release();
        }
    }

    async Task<bool> DoRemoveFactoryAsync( IActivityMonitor m, IMQTTChannelFactory factory )
    {
        int id = _factories.IndexOf( factory );
        if( id == -1 ) return false;
        _factories.RemoveAt( id );
        var state = _createAsync[id];
        if( state.HasValue )
        {
            await state.Value.cts.CancelAsync();
            try
            {
                await state.Value.task;
            }
            catch( OperationCanceledException ) { }
            catch( Exception e )
            {
                m.Error( $"Exception while stopping listening of {factory}.", e );
            }
        }
        _createAsync.RemoveAt( id );
        return true;
    }

    public void Dispose()
    {
        foreach( var factory in _factories )
        {
            factory.Dispose();
        }
    }
}
