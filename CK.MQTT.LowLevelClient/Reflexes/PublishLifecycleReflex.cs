using CK.MQTT.Client;
using CK.MQTT.Packets;
using CK.MQTT.Pumps;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT;

public class PublishLifecycleReflex : IReflexMiddleware
{
    readonly MessageExchanger _exchanger;

    public PublishLifecycleReflex( MessageExchanger exchanger )
    {
        _exchanger = exchanger;
    }

    public async ValueTask<(OperationStatus, bool)> ProcessIncomingPacketAsync( IMQTT3Sink sink,
        InputPump input, byte header, uint packetLength, PipeReader pipe, CancellationToken cancellationToken )
    {
        PacketType packetType = (PacketType)((header >> 4) << 4);//to remove right bits that may store flags data
        switch( packetType )
        {
            case PacketType.PublishAck:
                ushort? packetId2 = await pipe.ReadPacketIdPacketAsync( sink, packetLength, cancellationToken );
                if( !packetId2.HasValue ) return (OperationStatus.NeedMoreData, true);
                bool detectedDrop = _exchanger.LocalPacketStore.OnQos1Ack( sink, packetId2.Value, null );
                if( detectedDrop )
                {
                    _exchanger.OutputPump?.UnblockWriteLoop();
                }
                return (OperationStatus.Done, true);
            case PacketType.PublishComplete:
                ushort? packetId3 = await pipe.ReadPacketIdPacketAsync( sink, packetLength, cancellationToken );
                if( !packetId3.HasValue ) return (OperationStatus.NeedMoreData, true);
                _exchanger.LocalPacketStore.OnQos2AckStep2( packetId3.Value );
                return (OperationStatus.Done, true);
            case PacketType.PublishRelease:
                if( (header & 0b0010) != 2 ) throw new ProtocolViolationException( "MQTT-3.6.1-1 docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc384800427" );
                ushort? packetId = await pipe.ReadPacketIdPacketAsync( sink, packetLength, cancellationToken );
                if( !packetId.HasValue ) return (OperationStatus.NeedMoreData, true);
                await _exchanger.RemotePacketStore.RemoveIdAsync( packetId.Value );
                // We doesn't care of the return value, if the queue is filled we have too much job to do currently and we drop that packet.
                _exchanger.OutputPump?.TryQueueReflexMessage( LifecyclePacketV3.Pubcomp( packetId.Value ) );
                return (OperationStatus.Done, true);
            case PacketType.PublishReceived:
                ushort? packetId4 = await pipe.ReadPacketIdPacketAsync( sink, packetLength, cancellationToken );
                if( !packetId4.HasValue ) return (OperationStatus.NeedMoreData, true);
                IOutgoingPacket msg = await _exchanger.LocalPacketStore.OnQos2AckStep1Async( packetId4.Value );
                _exchanger.OutputPump?.TryQueueReflexMessage( msg );
                return (OperationStatus.Done, true);
            default:
                return (OperationStatus.Done, false);
        }
    }
}
