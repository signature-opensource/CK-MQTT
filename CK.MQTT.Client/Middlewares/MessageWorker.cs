using CK.Core;
using CK.MQTT.Client.Middleware;
using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public class MessageWorker : IAsyncDisposable
    {

        readonly Channel<object?> _events = Channel.CreateUnbounded<object?>( new UnboundedChannelOptions()
        {
            SingleReader = true,
            SingleWriter = false
        } );

        public List<IAgentMessageMiddleware> Middlewares { get; } = new List<IAgentMessageMiddleware>();

        public Task? WorkLoop { get; private set; }

        public ChannelWriter<object?> MessageWriter => _events.Writer;

        public MessageWorker()
        {
            WorkLoop = WorkLoopAsync( _events.Reader );
        }

        async Task WorkLoopAsync( ChannelReader<object?> channel )
        {
            ActivityMonitor m = new();
            await foreach( var item in channel.ReadAllAsync() )
            {
                await ProcessMessageAsync( m, item );
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
            await WorkLoop!;
            foreach( var middleware in Middlewares )
            {
                await middleware.DisposeAsync();
            }
        }
    }
}
