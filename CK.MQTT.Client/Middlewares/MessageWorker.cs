using CK.Core;
using CK.MQTT.Client.Middleware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client;

public class MessageWorker : IAsyncDisposable
{
    Task _workLoop = Task.CompletedTask;
    int _deathCounter = 0;
    readonly Channel<object?> _events = Channel.CreateUnbounded<object?>( new UnboundedChannelOptions()
    {
        SingleReader = true,
        SingleWriter = false
    } );

    public List<IAgentMessageMiddleware> Middlewares { get; } = new List<IAgentMessageMiddleware>();


    public void QueueMessage( object? message )
    {
        _events.Writer.TryWrite( message );
        var inc = Interlocked.Increment( ref _deathCounter );
        if( inc == 1 )
        {
            _workLoop = WorkLoopAsync();
        }
    }

    public MessageWorker()
    {
    }

    readonly ActivityMonitor _m = new();

    async Task WorkLoopAsync()
    {
        while( true )
        {
            var res = _events.Reader.TryRead( out var item );
            Debug.Assert( res );
            await ProcessMessageAsync( _m, item );
            var inc = Interlocked.Decrement( ref _deathCounter );
            if( inc == 0 ) break;
        }
    }

    async ValueTask ProcessMessageAsync( IActivityMonitor m, object? item )
    {
        foreach( var middleware in Middlewares )
        {
            if( await middleware.HandleAsync( m, item ) ) return;
        }
        m.Warn( $"No middleware has handled the agent message '{item}'" );
    }


    public async ValueTask DisposeAsync()
    {
        _events!.Writer.Complete();
        await _workLoop!;
        foreach( var middleware in Middlewares )
        {
            await middleware.DisposeAsync();
        }
    }
}
