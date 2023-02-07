using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class Timer : ITimer
    {
#pragma warning disable RS0030 // Do not used banned APIs
        readonly System.Threading.Timer _timer;

        public Timer( TimerCallback timerCallback )
        {
            _timer = new System.Threading.Timer( timerCallback );
        }

        public bool Change( int dueTime, int period ) => _timer.Change( dueTime, period );

        public bool Change( long dueTime, long period ) => _timer.Change( dueTime, period );

        public bool Change( TimeSpan dueTime, TimeSpan period ) => _timer.Change( dueTime, period );

        public bool Change( uint dueTime, uint period ) => _timer.Change( dueTime, period );

        public void Dispose() => _timer.Dispose();

        public ValueTask DisposeAsync() => _timer.DisposeAsync();
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
