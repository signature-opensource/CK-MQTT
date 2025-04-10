using CK.MQTT.Client;
using CK.MQTT.Pumps;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT;

public class UnsubackReflex : IReflexMiddleware
{
    readonly MessageExchanger _exchanger;

    public UnsubackReflex( MessageExchanger exchanger )
    {
        _exchanger = exchanger;
    }

    public async ValueTask<(OperationStatus, bool)> ProcessIncomingPacketAsync( IMQTT3Sink sink, InputPump sender, byte header, uint packetLength, PipeReader pipeReader, CancellationToken cancellationToken )
    {
        if( PacketType.UnsubscribeAck != (PacketType)header )
        {
            return (OperationStatus.Done, false);
        }
        ushort? packetId = await pipeReader.ReadPacketIdPacketAsync( sink, packetLength, cancellationToken );
        if( !packetId.HasValue ) return (OperationStatus.NeedMoreData, true);
        bool detectedDrop = _exchanger.LocalPacketStore.OnQos1Ack( sink, packetId.Value, null );
        if( detectedDrop )
        {
            _exchanger.OutputPump?.UnblockWriteLoop();
        }
        return (OperationStatus.Done, true);
    }
}
