using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class UnsubackReflex : IReflexMiddleware
    {
        readonly PacketStore _store;

        public UnsubackReflex( PacketStore store )
        {
            _store = store;
        }
        public async ValueTask ProcessIncomingPacketAsync( IMqttLogger m, IncomingMessageHandler sender,
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
