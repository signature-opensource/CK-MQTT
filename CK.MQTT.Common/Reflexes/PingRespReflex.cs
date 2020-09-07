using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class PingRespReflex : IReflexMiddleware
    {
        readonly MqttConfiguration _config;
        readonly InputPump _incomingMessageHandler;
        readonly Timer _timer;
        public PingRespReflex( MqttConfiguration config, InputPump incomingMessageHandler )
        {
            _config = config;
            _incomingMessageHandler = incomingMessageHandler;
            _timer = new Timer( TimerCallback );
        }

        void TimerCallback( object? state )
        {
            _incomingMessageHandler.SetTimeout( ( m ) => m?.PingReqTimeout() );
        }

        public void StartPingTimeoutTimer()
        {
            if( _config.KeepAliveSecs > 0 ) _timer.Change( _config.KeepAliveSecs * 1000, Timeout.Infinite );
        }

        public async ValueTask ProcessIncomingPacketAsync( IInputLogger? m, InputPump sender,
            byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            if( PacketType.PingResponse != (PacketType)header )
            {
                await next();
                return;
            }
            using( m?.ProcessPacket( PacketType.PingResponse ) )
            {

                _timer.Change( Timeout.Infinite, Timeout.Infinite );//Abort timer
                await pipeReader.BurnBytes( packetLength );
            }
        }
    }
}
