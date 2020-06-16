using CK.Core;
using CK.MQTT.Client.OutgoingPackets;
using CK.MQTT.Common;
using CK.MQTT.Common.Channels;
using System;
using System.Threading;

namespace CK.MQTT.Client
{
    class KeepAliveTimer
    {
        readonly IActivityMonitor _m;
        readonly TimeSpan _keepAlive;
        readonly TimeSpan _pingRespTimeout;
        readonly Timer _timer;
        bool _timeoutMode;
        public OutgoingMessageHandler? OutgoingMessageHandler { get; set; }
        public Action<IActivityMonitor>? TimeoutCallback { get; set; }
        public KeepAliveTimer( TimeSpan keepAlive, TimeSpan pingRespTimeout )
        {
            _m = new ActivityMonitor();
            _keepAlive = keepAlive;
            _pingRespTimeout = pingRespTimeout;
            _timer = new Timer( TimerDone );
        }

        void TimerDone( object? state )
        {
            if( _timeoutMode )
            {
                TimeoutCallback!(_m);
            }
            _timer.Change( _pingRespTimeout, TimeSpan.FromMilliseconds( -1 ) );
            _timeoutMode = true;
            OutgoingMessageHandler!.QueueReflexMessage( new OutgoingPingReq() );
        }

        public OutgoingPacket OutputTransformer( IActivityMonitor m, OutgoingPacket outgoingPacket )
        {
            ResetTimer();
            return outgoingPacket;
        }

        public void ResetTimer()
        {
            _timer.Change( _keepAlive, TimeSpan.FromMilliseconds( -1 ) );
            _timeoutMode = false;
        }
    }
}
