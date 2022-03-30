using CK.MQTT.Client;
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
        readonly ILocalPacketStore _store;

        public UnsubackReflex( ILocalPacketStore store ) => _store = store;

        public async ValueTask<OperationStatus> ProcessIncomingPacketAsync( IMqtt3Sink sink, InputPump sender, byte header, uint packetLength, PipeReader pipeReader, Func<ValueTask<OperationStatus>> next, CancellationToken cancellationToken )
        {
            if( PacketType.UnsubscribeAck != (PacketType)header )
            {
                return await next();
            }
            ushort? packetId = await pipeReader.ReadPacketIdPacketAsync( sink, packetLength, cancellationToken );
            if( !packetId.HasValue ) return OperationStatus.NeedMoreData;
            await _store.OnQos1AckAsync( sink, packetId.Value, null );
            return OperationStatus.Done;
        }
    }
}
