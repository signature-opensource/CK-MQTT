using CK.Core;
using CK.MQTT.Abstractions.Serialisation;
using CK.MQTT.Client.Deserialization;
using CK.MQTT.Common;
using CK.MQTT.Common.Packets;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
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
        public ValueTask ProcessIncomingPacketAsync( IActivityMonitor m, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            if( PacketType.PingResponse != (PacketType)header )
            {
                return next();
            }
            _callback?.Invoke();
            return new ValueTask();
        }
    }
}
