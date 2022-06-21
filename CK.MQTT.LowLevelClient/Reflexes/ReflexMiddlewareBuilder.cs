using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace CK.MQTT
{
    /// <summary>
    /// Help to build a <see cref="Reflex"/> out of multiple <see cref="ReflexMiddleware"/>.
    /// </summary>
    public class ReflexMiddlewareBuilder
    {
        bool _built = false;
        /// <summary>
        /// The chain of middleware.
        /// </summary>
        readonly List<IReflexMiddleware> _reflexes = new();

        /// <summary>
        /// Append this middleware to the chain of <see cref="IReflexMiddleware"/>.
        /// </summary>
        /// <param name="reflex">The reflex to use.</param>
        /// <returns>The <see cref="ReflexMiddleware"/> instance.</returns>
        public ReflexMiddlewareBuilder UseMiddleware( IReflexMiddleware reflex )
        {
            if( _built ) throw new InvalidOperationException("UseMiddleware called after being built.");
            _reflexes.Add( reflex );
            return this;
        }

        /// <summary>
        /// Build a <see cref="Reflex"/> from the chain of <see cref="ReflexMiddleware"/>.
        /// </summary>
        /// <param name="lastReflex">The 'next' of the last <see cref="ReflexMiddleware"/> will be this delegate.</param>
        /// <returns>The middleware chain built as a <see cref="Reflex"/>.</returns>
        public Reflex Build()
        {
            _built = true;
            return new( _reflexes.ToArray() );
        }
    }
}
