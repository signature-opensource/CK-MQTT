using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class UnsubackReflex : IReflexMiddleware
    {
        readonly IPacketStore _store;

        public UnsubackReflex( IPacketStore store ) => _store = store;
        public async ValueTask ProcessIncomingPacketAsync( IInputLogger? m, InputPump sender, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next, CancellationToken cancellationToken )
        {
            if( PacketType.UnsubscribeAck != (PacketType)header )
            {
                await next();
                return;
            }
            using( m?.ProcessPacket( PacketType.UnsubscribeAck ) )
            {
                ushort packetId = await pipeReader.ReadPacketIdPacket( m, packetLength );
                await _store.OnMessageAck( m, packetId );
            }
        }
    }
}
