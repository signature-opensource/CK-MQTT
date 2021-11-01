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

        public CancellationTokenSource Create( CancellationToken linkedToken, int millisecondsDelay )
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource( linkedToken );
            cts.CancelAfter( millisecondsDelay );
            return cts;
        }

        public CancellationTokenSource Create( CancellationToken linkedToken, TimeSpan delay )
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource( linkedToken );
            cts.CancelAfter( delay );
            return cts;
        }
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
