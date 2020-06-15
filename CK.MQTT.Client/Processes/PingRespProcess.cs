using CK.Core;
using CK.MQTT.Common;
using System;
using System.Threading;

namespace CK.MQTT.Client.Processes
{
    class PingRespProcess : IDisposable
    {
        readonly Timer _timer;
        readonly TimeSpan _timeSpan;
        readonly Action _timeoutCallback;

        public PingRespProcess( TimeSpan timeSpan, Action timeoutCallback )
        {
            _timer = new Timer( TimerCallback );
            _timer.Change( timeSpan, TimeSpan.FromMilliseconds( -1 ) );
            _timeSpan = timeSpan;
            _timeoutCallback = timeoutCallback;
        }

        void TimerCallback( object? state ) => _timeoutCallback();

        OutgoingPacket OutputTransformer( IActivityMonitor m, OutgoingPacket outgoingPacket )
        {
            _timer.Change( _timeSpan, TimeSpan.FromMilliseconds( -1 ) );
            return outgoingPacket;
        }

        public void Dispose() => _timer.Dispose();
    }
}
