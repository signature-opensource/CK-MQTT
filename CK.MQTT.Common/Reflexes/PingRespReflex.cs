using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class PingRespReflex : IReflexMiddleware
    {
        readonly MqttConfiguration _config;
        readonly IncomingMessageHandler _incomingMessageHandler;
        readonly Timer _timer;
        public PingRespReflex( MqttConfiguration config, IncomingMessageHandler incomingMessageHandler )
        {
            _config = config;
            _incomingMessageHandler = incomingMessageHandler;
            _timer = new Timer( TimerCallback );
        }

        void TimerCallback( object? state )
        {
            _incomingMessageHandler.SetTimeout( ( m ) => m.Error( "The broker did not responded PingReq in the given amount of time." ) );
        }

        public void StartPingTimeoutTimer()
        {
            if( _config.KeepAliveSecs > 0 ) _timer.Change( _config.KeepAliveSecs * 1000, Timeout.Infinite );
        }

        public ValueTask ProcessIncomingPacketAsync( IMqttLogger m, IncomingMessageHandler sender,
            byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            if( PacketType.PingResponse != (PacketType)header ) return next();
            m.Trace( $"Handling incoming packet as {PacketType.PingResponse}." );
            _timer.Change( Timeout.Infinite, Timeout.Infinite );//Abort timer
            ValueTask valueTask = pipeReader.BurnBytes( packetLength );
            return valueTask;
        }
    }
}
