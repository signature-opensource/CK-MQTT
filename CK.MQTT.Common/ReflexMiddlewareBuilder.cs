using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Middleware that process incoming packets. See <see cref="Reflex"/> for more information.
    /// </summary>
    /// <param name="m">The logger to log activities while processing the incoming packet.</param>
    /// <param name="sender">The <see cref="IncomingMessageHandler"/> that called this middleware.</param>
    /// <param name="header">The first byte of the packet.</param>
    /// <param name="packetLength">The length of the incoming packet.</param>
    /// <param name="pipeReader">The pipe reader to use to read the packet data.</param>
    /// <param name="next">The next middleware.</param>
    /// <remarks>
    /// If a middleware advance the <see cref="PipeReader"/>, the next middleware can't be aware of it.
    /// </remarks>
    /// <returns>A <see cref="ValueTask"/> that complete when the middleware finished it's job.</returns>
    public delegate ValueTask ReflexMiddleware(
        IMqttLogger m, IncomingMessageHandler sender, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next
    );

    /// <summary>
    /// An interface exposing a method method that is a <see cref="ReflexMiddleware"/>.
    /// </summary>
    public interface IReflexMiddleware
    {
        /// <inheritdoc cref="ReflexMiddleware"/>
        ValueTask ProcessIncomingPacketAsync(
            IMqttLogger m, IncomingMessageHandler sender,
        byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next );
    }

    /// <summary>
    /// Help to build a <see cref="Reflex"/> out of multiple <see cref="ReflexMiddleware"/>.
    /// </summary>
    public class ReflexMiddlewareBuilder
    {
        /// <summary>
        /// The chain of middleware.
        /// </summary>
        readonly List<ReflexMiddleware> _reflexes = new List<ReflexMiddleware>();

        ReflexMiddlewareBuilder Use( ReflexMiddleware reflex )
        {
            _reflexes.Add( reflex );
            return this;
        }

        /// <summary>
        /// Append this middleware to the chain of <see cref="ReflexMiddleware"/>.
        /// </summary>
        /// <param name="reflex">The reflex to use.</param>
        /// <returns>The <see cref="ReflexMiddleware"/> instance.</returns>
        public ReflexMiddlewareBuilder UseMiddleware( IReflexMiddleware reflex ) => Use( reflex.ProcessIncomingPacketAsync );

        /// <inheritdoc cref="UseMiddleware(IReflexMiddleware)"/>
        public ReflexMiddlewareBuilder UseMiddleware( ReflexMiddleware reflex ) => Use( reflex );

        /// <summary>
        /// Build a <see cref="Reflex"/> from the chain of <see cref="ReflexMiddleware"/>.
        /// </summary>
        /// <param name="lastReflex">The 'next' of the last <see cref="ReflexMiddleware"/> will be this delegate.</param>
        /// <returns>The middleware chain built as a <see cref="Reflex"/>.</returns>
        public Reflex Build( Reflex lastReflex )
        {
            foreach( var curr in _reflexes.Reverse<ReflexMiddleware>() )
            {
                Reflex previousReflex = lastReflex;
                //Here some closure black magics. A lot of mind bending stuff happen if you inline this variable, and make it not work.
                //TODO: A better implementation would not use a closure, to be more explicit.
                Reflex newMiddleware = ( IMqttLogger m, IncomingMessageHandler s, byte h, int l, PipeReader p ) //We create a lambda that...
                    => curr( m, s, h, l, p, () => previousReflex( m, s, h, l, p ) );// Call current the middleware, with a callback to the previous previous middleware.
                lastReflex = newMiddleware;
            }
            return lastReflex;
        }
    }
}
