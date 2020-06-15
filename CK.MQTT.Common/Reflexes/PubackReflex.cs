using CK.Core;
using CK.MQTT.Abstractions.Serialisation;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using CK.MQTT.Common.Stores;
using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Reflexes
{
    public class PubackReflex : IReflexMiddleware
    {
        readonly IPacketStore _store;

        public PubackReflex( IPacketStore store )
        {
            _store = store;
        }
        public async ValueTask ProcessIncomingPacketAsync( IActivityMonitor m, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            if( PacketType.PublishAck != (PacketType)header )
            {
                await next();
                return;
            }
            ushort packetId = await pipeReader.ReadUInt16();
            await _store.FreePacketIdAsync( m, packetId );
        }
    }
}
