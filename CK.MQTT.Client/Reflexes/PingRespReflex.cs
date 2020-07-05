using System;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    class PingRespReflex : IReflexMiddleware
    {
        readonly Action? _callback;

        public PingRespReflex( Action? callback )
        {
            _callback = callback;
        }
        public ValueTask ProcessIncomingPacketAsync( IActivityMonitor m, IncomingMessageHandler sender,
            byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            if( PacketType.PingResponse != (PacketType)header )
            {
                return next();
            }
            m.Trace( $"Handling incoming packet as {PacketType.PingResponse}." );
            ValueTask valueTask = pipeReader.BurnBytes( packetLength );
            _callback?.Invoke();
            return valueTask;
        }
    }
}
