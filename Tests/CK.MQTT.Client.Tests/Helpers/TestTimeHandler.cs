using CK.MQTT.LowLevelClient.Time;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers;

public class TestTimeHandler : ITimeUtilities
{
    readonly object _lock = new();
    readonly List<WeakReference<TestStopwatch>> _stopwatches = new();
    readonly List<CTS> _cts = new();
    readonly List<Delays> _delays = new();
    const BindingFlags _bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly FieldInfo? _isDisposedField = typeof( CancellationTokenSource ).GetField( "_disposed", _bindingFlags );
    private readonly List<TestTimer> _timers = new();
    DateTime _currentTime = DateTime.UtcNow;
    public DateTime UtcNow => _currentTime;

    public void IncrementTime( TimeSpan timeSpan )
    {
        lock( _lock )
        {
            _currentTime += timeSpan;
            _stopwatches.ForEach( s =>
            {
                if( s.TryGetTarget( out TestStopwatch? stopwatch ) )
                {
                    stopwatch.IncrementTime( timeSpan );
                }
            } );

            _timers.ForEach( s => s.IncrementTime( timeSpan ) );

            _stopwatches.RemoveAll( s => !s.TryGetTarget( out _ ) );

            _cts.RemoveAll( s => !s.IncrementTime( timeSpan )
                || (bool)_isDisposedField!.GetValue( s.CancellationTokenSource )!
                || s.CancellationTokenSource.IsCancellationRequested
            );
            foreach( var delay in _delays )
            {
                delay.TimeSpan -= timeSpan;
            }
            _delays.RemoveAll( delay =>
            {
                if( delay.TimeSpan.TotalMilliseconds <= 0 )
                {
                    delay.Tcs.TrySetResult();
                    return true;
                }
                return false;
            } );
        }
    }
    class Delays
    {
        public TaskCompletionSource Tcs;
        public TimeSpan TimeSpan;

        public Delays( TaskCompletionSource tcs, TimeSpan timeSpan )
        {
            Tcs = tcs;
            TimeSpan = timeSpan;
        }
    }
    class DelayTask
    {
        public readonly TaskCompletionSource TaskCompletionSource;
        public TimeSpan TimeUntilCompletion;

        public DelayTask( TimeSpan timeUntilCompletion, CancellationToken cancellationToken = default )
        {
            // If the continuation is synchronous, it may avoid concurrencies issues that we want to catch.
            TaskCompletionSource = new( TaskCreationOptions.RunContinuationsAsynchronously );
            cancellationToken.Register( () => TaskCompletionSource.TrySetCanceled() );
            TimeUntilCompletion = timeUntilCompletion;
        }

        public void AdvanceTime( TimeSpan timeSpan )
        {
            if( TaskCompletionSource.Task.IsCompleted ) return;
            TimeUntilCompletion -= timeSpan;
            if( TimeUntilCompletion.Ticks < 0 ) TaskCompletionSource.SetResult();
        }
    }
    class TestStopwatch : IStopwatch
    {
        public void IncrementTime( TimeSpan timeSpan )
        {
            if( !IsRunning ) return;
            Elapsed += timeSpan;
        }

        public TimeSpan Elapsed { get; private set; }

        public long ElapsedMilliseconds => (long)Elapsed.TotalMilliseconds;

        public long ElapsedTicks => Elapsed.Ticks;

        public bool IsRunning { get; private set; }

        public void Reset()
        {
            IsRunning = false;
            Elapsed = new TimeSpan();
        }

        public void Restart() => Elapsed = new TimeSpan();

        public void Start() => IsRunning = true;

        public void Stop() => IsRunning = false;
    }
    class CTS
    {
        public CTS( TimeSpan delayToCancel )
        {
            DelayToCancel = delayToCancel;
            CancellationTokenSource = new();
        }

        public CTS( TimeSpan delayToCancel, CancellationToken linkedToken )
        {
            DelayToCancel = delayToCancel;
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource( linkedToken );
        }
        public CancellationTokenSource CancellationTokenSource { get; }
        public TimeSpan DelayToCancel { get; private set; }

        public bool IncrementTime( TimeSpan timeSpan )
        {
            DelayToCancel -= timeSpan;
            try
            {
                if( DelayToCancel.TotalMilliseconds <= 0 ) CancellationTokenSource.Cancel();
            }
            catch( ObjectDisposedException )
            {
                return false;
            }
            return true;
        }
    }
    class TestTimer : ITimer
    {
        TimeSpan _timeSpan = Timeout.InfiniteTimeSpan;
        TimeSpan _dueTime = Timeout.InfiniteTimeSpan;

        public TestTimer( TimerCallback timerCallback )
        {
            _timerCallback = timerCallback;
        }

        public bool Change( int dueTime, int period ) => Change( (long)dueTime, period );

        public bool Change( long dueTime, long period )
        {
            if( period != Timeout.Infinite ) throw new NotImplementedException();
            _dueTime = TimeSpan.FromMilliseconds( dueTime );
            _timeSpan = TimeSpan.Zero;
            return true;
        }

        public bool Change( TimeSpan dueTime, TimeSpan period )
        {
            if( period != Timeout.InfiniteTimeSpan ) throw new NotImplementedException();
            _dueTime = dueTime;
            _timeSpan = TimeSpan.Zero;
            return true;
        }

        public bool Change( uint dueTime, uint period ) => Change( (long)dueTime, period );

        bool _dispose;
        readonly TimerCallback _timerCallback;

        public void Dispose()
        {
            if( _dispose ) throw new InvalidOperationException( "Double dispose" );
            _dispose = true;
        }

        public ValueTask DisposeAsync()
        {
            if( _dispose ) throw new InvalidOperationException( "Double dispose" );
            _dispose = true;
            return new ValueTask();
        }

        public void IncrementTime( TimeSpan time )
        {
            _timeSpan += time;
            if( _dueTime == Timeout.InfiniteTimeSpan ) return;
            if( _timeSpan >= _dueTime )
            {
                _timerCallback( null );
                _dueTime = Timeout.InfiniteTimeSpan;
            }
        }
    }

    public IStopwatch CreateStopwatch()
    {
        TestStopwatch stopwatch = new();
        lock( _lock )
        {
            _stopwatches.Add( new WeakReference<TestStopwatch>( stopwatch ) );
        }
        return stopwatch;
    }

    public CancellationTokenSource CreateCTS( int millisecondsDelay )
        => CreateCTS( TimeSpan.FromMilliseconds( millisecondsDelay ) );
    public CancellationTokenSource CreateCTS( TimeSpan delay )
    {
        lock( _lock )
        {
            CTS cts = new( delay );
            _cts.Add( cts );
            return cts.CancellationTokenSource;
        }
    }

    public CancellationTokenSource CreateCTS( CancellationToken linkedToken, int millisecondsDelay )
    {
        lock( _lock )
        {
            CTS cts = new( TimeSpan.FromMilliseconds( millisecondsDelay ), linkedToken );
            _cts.Add( cts );
            return cts.CancellationTokenSource;
        }
    }

    public CancellationTokenSource CreateCTS( CancellationToken linkedToken, TimeSpan delay )
    {
        lock( _lock )
        {
            CTS cts = new( delay, linkedToken );
            _cts.Add( cts );
            return cts.CancellationTokenSource;
        }
    }

    public ITimer CreateTimer( TimerCallback timerCallback )
    {
        var timer = new TestTimer( timerCallback );
        _timers.Add( timer );
        return timer;
    }

    public Task Delay( TimeSpan timeSpan )
    {
        var tcs = new TaskCompletionSource();
        _delays.Add( new Delays( tcs, timeSpan ) );
        return tcs.Task;
    }
}
