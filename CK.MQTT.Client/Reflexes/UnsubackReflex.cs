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
    class UnsubackReflex : IReflexMiddleware
    {
        readonly Action<ushort> _callback;

        public UnsubackReflex( Action<ushort> callback )
        {
            _callback = callback;
        }
        public async ValueTask ProcessIncomingPacketAsync( IActivityMonitor m, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            if( PacketType.UnsubscribeAck != (PacketType)header )
            {
                await next();
                return;
            }
            ushort packetId = await pipeReader.ReadUInt16();
            _callback( packetId );
        }
    }
}
