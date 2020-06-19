using CK.Core;
using CK.MQTT.Common.Channels;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading.Tasks;

namespace CK.MQTT.Common
{
    public delegate ValueTask ReflexMiddleware(
        IActivityMonitor m, IncomingMessageHandler sender, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next
    );

    public interface IReflexMiddleware
    {
        ValueTask ProcessIncomingPacketAsync(
            IActivityMonitor m, IncomingMessageHandler sender,
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
                lastReflex = ( IActivityMonitor m, IncomingMessageHandler s, byte h, int l, PipeReader p )
                    => curr( m, s, h, l, p, () => lastReflex( m, s, h, l, p ) );
            }
            return lastReflex;
        }
    }
}
