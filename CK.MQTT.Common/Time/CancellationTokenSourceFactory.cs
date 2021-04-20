using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace CK.MQTT.Common.Time
{
    public class CancellationTokenSourceFactory : ICancellationTokenSourceFactory
    {

#pragma warning disable RS0030 // Do not used banned APIs
        /// <inheritdoc/>
        public CancellationTokenSource Create( int millisecondsDelay ) => new( millisecondsDelay );
        /// <inheritdoc/>
        public CancellationTokenSource Create( TimeSpan delay ) => new( delay );
#pragma warning restore RS0030 // Do not used banned APIs

    }
}
