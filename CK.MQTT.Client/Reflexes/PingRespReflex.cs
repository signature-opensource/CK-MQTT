using CK.Core;
using CK.MQTT.Abstractions.Serialisation;
using CK.MQTT.Common;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Reflexes
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
