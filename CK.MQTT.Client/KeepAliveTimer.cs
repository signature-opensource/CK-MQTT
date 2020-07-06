using System;
using System.Threading;

namespace CK.MQTT
{
    class KeepAliveTimer
    {
        readonly IMqttLogger _m;
        readonly TimeSpan _keepAlive;
        readonly TimeSpan _pingRespTimeout;
        readonly OutgoingMessageHandler _output;
        readonly Timer _timer;
        bool _timeoutMode;
        readonly Action<IMqttLogger> _timeoutCallback;
        public KeepAliveTimer( IMqttLoggerFactory loggerFactory, MqttConfiguration config, OutgoingMessageHandler output, Action<IMqttLogger> timeoutCallback )
        {
            _m = loggerFactory.Create();
            _keepAlive = TimeSpan.FromSeconds( config.KeepAliveSecs );
            _pingRespTimeout = TimeSpan.FromMilliseconds( config.WaitTimeoutMs );
            _output = output;
            _timeoutCallback = timeoutCallback;
            _timer = new Timer( TimerDone );
        }

        void TimerDone( object? state )
        {
            if( _timeoutMode )
            {
                _timeoutCallback!( _m );
            }
            _timer.Change( _pingRespTimeout, TimeSpan.FromMilliseconds( -1 ) );
            _timeoutMode = true;
            _output.QueueReflexMessage( new OutgoingPingReq() );
        }

        public IOutgoingPacket OutputTransformer( IMqttLogger m, IOutgoingPacket outgoingPacket )
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
