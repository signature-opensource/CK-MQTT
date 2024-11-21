using CK.MQTT.LowLevelClient.Time;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT;

class DefaultTimeUtilities : ITimeUtilities
{
#pragma warning disable RS0030 // Do not used banned APIs
    public IStopwatch CreateStopwatch() => new Stopwatch( new System.Diagnostics.Stopwatch() );


    /// <inheritdoc/>
    public CancellationTokenSource CreateCTS( int millisecondsDelay ) => new( millisecondsDelay );
    /// <inheritdoc/>
    public CancellationTokenSource CreateCTS( TimeSpan delay ) => new( delay );

    public CancellationTokenSource CreateCTS( CancellationToken linkedToken, int millisecondsDelay )
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource( linkedToken );
        cts.CancelAfter( millisecondsDelay );
        return cts;
    }

    public CancellationTokenSource CreateCTS( CancellationToken linkedToken, TimeSpan delay )
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource( linkedToken );
        cts.CancelAfter( delay );
        return cts;
    }
    public DateTime UtcNow => DateTime.UtcNow;

    public ITimer CreateTimer( TimerCallback timerCallback ) => new Timer( timerCallback );

    public Task Delay( TimeSpan timeSpan ) => Task.Delay( timeSpan );
#pragma warning restore RS0030 // Do not used banned APIs
}
