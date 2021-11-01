using CK.MQTT.Common.Time;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    public class TestDelayHandler : IDelayHandler, IStopwatchFactory, ICancellationTokenSourceFactory
    {
        readonly object _lock = new();
        readonly List<DelayTask> _delays = new();
        readonly List<WeakReference<Stopwatch>> _stopwatches = new();
        readonly List<CTS> _cts = new();
        public void IncrementTime( TimeSpan timeSpan )
        {
            lock( _lock )
            {
                _stopwatches.ForEach( s =>
                {
                    if( s.TryGetTarget( out Stopwatch? stopwatch ) )
                    {
                        stopwatch.IncrementTime( timeSpan );
                    }
                } );
                _delays.ForEach( s => s.AdvanceTime( timeSpan ) );

                _stopwatches.RemoveAll( s => !s.TryGetTarget( out _ ) );
                _cts.RemoveAll( s => !s.IncrementTime( timeSpan ) || s.CancellationTokenSource.IsCancellationRequested );
                _delays.RemoveAll( s => s.TaskCompletionSource.Task.IsCompleted );
            }
        }

        class DelayTask
        {
            public readonly TaskCompletionSource<object?> TaskCompletionSource;
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
                if( TimeUntilCompletion.Ticks < 0 ) TaskCompletionSource.SetResult( null );
            }
        }

        class Stopwatch : IStopwatch
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
                    if( DelayToCancel.TotalMilliseconds < 0 ) CancellationTokenSource.Cancel();
                }
                catch( ObjectDisposedException )
                {
                    return false;
                }
                return true;
            }
        }

        public IStopwatch Create()
        {
            Stopwatch stopwatch = new();
            lock( _lock )
            {
                _stopwatches.Add( new WeakReference<Stopwatch>( stopwatch ) );
            }
            return stopwatch;
        }

        public Task Delay( int msDelay ) => Delay( TimeSpan.FromMilliseconds( msDelay ), default );

        public Task Delay( int msDelay, CancellationToken cancelToken ) => Delay( TimeSpan.FromMilliseconds( msDelay ), cancelToken );

        public Task Delay( TimeSpan delay ) => Delay( delay, default );

        public Task Delay( TimeSpan delay, CancellationToken cancellationToken )
        {
            var delayTask = new DelayTask( delay, cancellationToken );
            lock( _lock )
            {
                _delays.Add( delayTask );
            }
            return delayTask.TaskCompletionSource.Task;
        }

        public CancellationTokenSource Create( int millisecondsDelay ) => Create( TimeSpan.FromMilliseconds( millisecondsDelay ) );
        public CancellationTokenSource Create( TimeSpan delay )
        {
            lock( _lock )
            {
                CTS cts = new( delay );
                _cts.Add( cts );
                return cts.CancellationTokenSource;
            }
        }

        public CancellationTokenSource Create( CancellationToken linkedToken, int millisecondsDelay )
        {
            lock( _lock )
            {
                CTS cts = new( TimeSpan.FromMilliseconds( millisecondsDelay ), linkedToken );
                _cts.Add( cts );
                return cts.CancellationTokenSource;
            }
        }

        public CancellationTokenSource Create( CancellationToken linkedToken, TimeSpan delay )
        {
            lock( _lock )
            {
                CTS cts = new( delay, linkedToken );
                _cts.Add( cts );
                return cts.CancellationTokenSource;
            }
        }
    }
}
