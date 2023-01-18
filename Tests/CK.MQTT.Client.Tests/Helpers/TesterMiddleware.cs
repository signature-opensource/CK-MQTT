using CK.Core;
using CK.MQTT.Client.Middleware;
using NUnit.Framework;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    class TesterMiddleware : IAgentMessageMiddleware
    {
        readonly ChannelWriter<object?> _eventWriter;

        public TesterMiddleware( ChannelWriter<object?> eventWriter ) => _eventWriter = eventWriter;

        public ValueTask DisposeAsync() => new ValueTask();

        public async ValueTask<bool> HandleAsync( IActivityMonitor m, object? item )
        {
            if( item is VolatileApplicationMessage msg )
            {
                var buffer = msg.Message.Payload.ToArray();
                var appMessage = new VolatileApplicationMessage(
                    new ApplicationMessage( msg.Message.Topic, buffer, msg.Message.QoS, msg.Message.Retain ), new DisposableComposite()
                );
                await _eventWriter.WriteAsync( appMessage );
            }
            else
            {
                await _eventWriter.WriteAsync( item );
            }
            return false;
        }
    }
}
