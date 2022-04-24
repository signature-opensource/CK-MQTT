using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.LowLevelClient.Time
{
    public interface ITimeUtilities
    {
        IStopwatch CreateStopwatch();

#pragma warning disable RS0030 // Do not used banned APIs
        /// <inheritdoc cref="CancellationTokenSource(int)"/>
        CancellationTokenSource CreateCTS( int millisecondsDelay );
        /// <inheritdoc cref="CancellationTokenSource(TimeSpan)"/>
        CancellationTokenSource CreateCTS( TimeSpan delay );
#pragma warning restore RS0030 // Do not used banned APIs

#pragma warning disable CA1068 // Motive: Special API about CancellationToken.
        CancellationTokenSource CreateCTS( CancellationToken linkedToken, int millisecondsDelay );
        CancellationTokenSource CreateCTS( CancellationToken linkedToken, TimeSpan delay );
#pragma warning restore CA1068

        DateTime UtcNow { get; }

        ITimer CreateTimer( TimerCallback timerCallback );
    }
}
