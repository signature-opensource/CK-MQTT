using CK.MQTT.Pumps;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Middleware that process incoming packets. See <see cref="Reflex"/> for more information.
    /// </summary>
    /// <param name="m">The logger to log activities while processing the incoming packet.</param>
    /// <param name="sender">The <see cref="InputPump"/> that called this middleware.</param>
    /// <param name="header">The first byte of the packet.</param>
    /// <param name="packetLength">The length of the incoming packet.</param>
    /// <param name="pipeReader">The pipe reader to use to read the packet data.</param>
    /// <param name="next">The next middleware.</param>
    /// <remarks>
    /// If a middleware advance the <see cref="PipeReader"/>, the next middleware can't be aware of it.
    /// </remarks>
    /// <returns>A <see cref="ValueTask"/> that complete when the middleware finished it's job.</returns>
    public delegate ValueTask<OperationStatus> ReflexMiddleware( IInputLogger? m, InputPump sender, byte header, uint packetLength, PipeReader pipeReader, Func<ValueTask<OperationStatus>> next, CancellationToken cancellationToken );

    /// <summary>
    /// An interface exposing a method method that is a <see cref="ReflexMiddleware"/>.
    /// </summary>
    public interface IReflexMiddleware
    {
        /// <inheritdoc cref="ReflexMiddleware"/>
        ValueTask<OperationStatus> ProcessIncomingPacketAsync( IInputLogger? m, InputPump sender, byte header, uint packetLength, PipeReader pipeReader, Func<ValueTask<OperationStatus>> next, CancellationToken cancellationToken );
    }

    /// <summary>
    /// Help to build a <see cref="Reflex"/> out of multiple <see cref="ReflexMiddleware"/>.
    /// </summary>
    public class ReflexMiddlewareBuilder
    {
        /// <summary>
        /// The chain of middleware.
        /// </summary>
        readonly List<ReflexMiddleware> _reflexes = new();

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
            => _reflexes.Reverse<ReflexMiddleware>()
            .Aggregate( lastReflex, ( previous, current ) => new Closure( current, previous ).RunAsync );


        readonly struct Closure
        {
            readonly ReflexMiddleware _current;
            readonly Reflex _previous;
            public Closure( ReflexMiddleware current, Reflex previous ) => (_current, _previous) = (current, previous);
            public ValueTask<OperationStatus> RunAsync( IInputLogger? m, InputPump s, byte h, uint l, PipeReader p, CancellationToken c )
            {
                Reflex previous = _previous;
                return _current( m, s, h, l, p, () => previous( m, s, h, l, p, c ), c );//TODO: there is a closure here :|
            }
        }
    }
}
