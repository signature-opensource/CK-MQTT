using CK.Core;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.MQTT.Common.Channels.IncomingMessageHandler;

namespace CK.MQTT.Common
{
    public delegate ValueTask ReflexMiddleware(
        IActivityMonitor m,
        byte header, int packetLength, PipeReader pipeReader,
        Func<ValueTask> next );

    public interface IReflexMiddleware
    {
        ValueTask ProcessIncomingPacket(
            IActivityMonitor m,
        byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next );
    }

    public class ReflexMiddlewareBuilder
    {
        readonly List<ReflexMiddleware> _reflexes = new List<ReflexMiddleware>();

        ReflexMiddlewareBuilder Use( ReflexMiddleware reflex)
        {
            _reflexes.Add( reflex );
            return this;
        }

        public ReflexMiddlewareBuilder UseMiddleware( IReflexMiddleware reflex ) => Use( reflex.ProcessIncomingPacket );

        public ReflexMiddlewareBuilder UseMiddleware( ReflexMiddleware reflex ) => Use( reflex );

        public Reflex Build( Reflex lastReflex )
        {
            foreach( var curr in _reflexes.Reverse<ReflexMiddleware>() )
            {
                lastReflex = ( IActivityMonitor m, byte h, int l, PipeReader p ) => curr( m, h, l, p, () => lastReflex( m, h, l, p ) );
            }
            return lastReflex;
        }
    }
}
