using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class UnsubackReflex : IReflexMiddleware
    {
        readonly IOutgoingPacketStore _store;

        public UnsubackReflex( IOutgoingPacketStore store ) => _store = store;
        public async ValueTask<OperationStatus> ProcessIncomingPacketAsync( IInputLogger? m, InputPump sender, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask<OperationStatus>> next, CancellationToken cancellationToken )
        {
            if( PacketType.UnsubscribeAck != (PacketType)header )
            {
                return await next();
            }
            using( m?.ProcessPacket( PacketType.UnsubscribeAck ) )
            {
                ushort? packetId = await pipeReader.ReadPacketIdPacketAsync( m, packetLength );
                if( !packetId.HasValue) return OperationStatus.NeedMoreData;
                await _store.OnQos1AckAsync( m, packetId.Value, null );
                return OperationStatus.Done;
            }
        }
    }
}
