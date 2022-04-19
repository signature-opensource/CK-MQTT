using System;
using System.Threading;

namespace CK.MQTT.Common.Time
{
    public interface ICancellationTokenSourceFactory
    {

#pragma warning disable RS0030 // Do not used banned APIs
        /// <inheritdoc cref="CancellationTokenSource(int)"/>
        CancellationTokenSource Create( int millisecondsDelay );
        /// <inheritdoc cref="CancellationTokenSource(TimeSpan)"/>
        CancellationTokenSource Create( TimeSpan delay );
#pragma warning restore RS0030 // Do not used banned APIs

#pragma warning disable CA1068 // Motive: Special API about CancellationToken.
        CancellationTokenSource Create( CancellationToken linkedToken, int millisecondsDelay );
        CancellationTokenSource Create( CancellationToken linkedToken, TimeSpan delay );
#pragma warning restore CA1068
    }
}
