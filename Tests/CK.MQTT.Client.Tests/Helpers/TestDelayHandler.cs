using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    class TestDelayHandler : IDelayHandler, IStopwatchFactory
    {
        readonly object _lock = new object();
        List<DelayTask> _delays { get; } = new List<DelayTask>();
        List<Stopwatch> _stopwatches { get; } = new List<Stopwatch>();

        public void IncrementTime( TimeSpan timeSpan )
        {
            lock( _lock )
            {
                _stopwatches.ForEach( s => s.IncrementTime( timeSpan ) );
                _delays.ForEach( s => s.AdvanceTime( timeSpan ) );
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

        public IStopwatch Create()
        {
            Stopwatch stopwatch = new();
            lock( _lock )
            {
                _stopwatches.Add( stopwatch );
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


    }
}
