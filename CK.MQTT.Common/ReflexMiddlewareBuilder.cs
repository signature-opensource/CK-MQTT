using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public delegate ValueTask ReflexMiddleware(
        IMqttLogger m, IncomingMessageHandler sender, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next
    );

    public interface IReflexMiddleware
    {
        ValueTask ProcessIncomingPacketAsync(
            IMqttLogger m, IncomingMessageHandler sender,
        byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next );
    }

    public class ReflexMiddlewareBuilder
    {
        readonly List<ReflexMiddleware> _reflexes = new List<ReflexMiddleware>();

        ReflexMiddlewareBuilder Use( ReflexMiddleware reflex )
        {
            _reflexes.Add( reflex );
            return this;
        }

        public ReflexMiddlewareBuilder UseMiddleware( IReflexMiddleware reflex ) => Use( reflex.ProcessIncomingPacketAsync );

        public ReflexMiddlewareBuilder UseMiddleware( ReflexMiddleware reflex ) => Use( reflex );

        public Reflex Build( Reflex lastReflex )
        {
            foreach( var curr in _reflexes.Reverse<ReflexMiddleware>() )
            {
                Reflex previousReflex = lastReflex;
                Reflex newMiddleware = ( IMqttLogger m, IncomingMessageHandler s, byte h, int l, PipeReader p ) //We create a lambda that...
                    =>
                {
                    return curr( m, s, h, l, p, // Call current the middleware
                    () => //With the previous previous middleware to call.
                    {
                        return previousReflex( m, s, h, l, p );
                    } );
                };
                lastReflex = newMiddleware;
            }
            return lastReflex;
        }
    }
}
