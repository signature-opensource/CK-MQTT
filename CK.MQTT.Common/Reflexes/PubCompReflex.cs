using CK.Core;
using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT.Common
{
    public class PubCompReflex : IReflexMiddleware
    {
        readonly PacketStore _store;

        public PubCompReflex( PacketStore store )
        {
            _store = store;
        }

        public async ValueTask ProcessIncomingPacketAsync( IActivityMonitor m, IncomingMessageHandler sender,
            byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            if( PacketType.PublishComplete != (PacketType)header )
            {
                await next();
                return;
            }
            m.Trace( $"Handling incoming packet as {PacketType.PublishComplete}." );
            ushort packetId = await pipeReader.ReadPacketIdPacket( m, packetLength );
            await _store.DiscardPacketIdAsync( m, packetId );
        }
    }
}
