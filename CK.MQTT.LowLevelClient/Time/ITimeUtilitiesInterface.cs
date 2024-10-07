using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.LowLevelClient.Time;

public interface ITimeUtilities
{
#pragma warning disable VSTHRD200 // .NET Type is named like this.
    Task Delay( TimeSpan timeSpan );
#pragma warning restore VSTHRD200
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
