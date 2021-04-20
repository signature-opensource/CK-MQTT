using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace CK.MQTT.Common.Time
{
    public interface ICancellationTokenSourceFactory
    {

#pragma warning disable RS0030 // Do not used banned APIs
        /// <inheritdoc cref="System.Threading.CancellationTokenSource.CancellationTokenSource(int)"/>
        CancellationTokenSource Create( int millisecondsDelay );
        /// <inheritdoc cref="System.Threading.CancellationTokenSource.CancellationTokenSource(TimeSpan)"/>
        CancellationTokenSource Create( TimeSpan delay );
#pragma warning restore RS0030 // Do not used banned APIs

    }
}
