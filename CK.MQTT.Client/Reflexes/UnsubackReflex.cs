using CK.Core;
using CK.MQTT.Abstractions.Serialisation;
using CK.MQTT.Common;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Stores;
using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Reflexes
{
    class UnsubackReflex : IReflexMiddleware
    {
        readonly PacketStore _store;

        public UnsubackReflex( PacketStore store )
        {
            _store = store;
        }
        public async ValueTask ProcessIncomingPacketAsync( IActivityMonitor m, IncomingMessageHandler sender,
            byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            if( PacketType.UnsubscribeAck != (PacketType)header )
            {
                await next();
                return;
            }
            m.Trace( $"Handling incoming packet as {PacketType.UnsubscribeAck}." );
            ushort packetId = await pipeReader.ReadPacketIdPacket( m, packetLength );
            await _store.DiscardMessageByIdAsync( m, packetId );
        }
    }
}
