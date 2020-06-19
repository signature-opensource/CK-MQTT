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
        readonly OutgoingMessageHandler _output;
        readonly Timer _timer;
        bool _timeoutMode;
        public Action<IActivityMonitor>? TimeoutCallback { get; set; }
        public KeepAliveTimer( TimeSpan keepAlive, TimeSpan pingRespTimeout, OutgoingMessageHandler output )
        {
            _m = new ActivityMonitor();
            _keepAlive = keepAlive;
            _pingRespTimeout = pingRespTimeout;
            _output = output;
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
            _output.QueueReflexMessage( new OutgoingPingReq() );
        }

        public IOutgoingPacket OutputTransformer( IActivityMonitor m, IOutgoingPacket outgoingPacket )
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
