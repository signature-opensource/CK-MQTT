using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class PublishLifecycleReflex : IReflexMiddleware
    {
        readonly IIncomingPacketStore _packetIdStore;
        readonly IOutgoingPacketStore _store;
        readonly OutputPump _output;

        public PublishLifecycleReflex( IIncomingPacketStore packetIdStore, IOutgoingPacketStore store, OutputPump output )
            => (_packetIdStore, _store, _output) = (packetIdStore, store, output);

        public async ValueTask<OperationStatus> ProcessIncomingPacketAsync(
            IInputLogger? m, InputPump input, byte header, int pktLen, PipeReader pipe, Func<ValueTask<OperationStatus>> next, CancellationToken cancellationToken )
        {
            PacketType packetType = (PacketType)((header >> 4) << 4);//to remove right bits that may store flags data
            switch( packetType )
            {
                case PacketType.PublishAck:
                case PacketType.PublishComplete:
                case PacketType.PublishRelease:
                case PacketType.PublishReceived:
                    break;
                default:
                    return await next();
            }
            using( m?.ProcessPacket( packetType ) )
            {
                switch( packetType )
                {
                    case PacketType.PublishAck:
                        ushort? packetId2 = await pipe.ReadPacketIdPacketAsync( m, pktLen, cancellationToken );
                        if( !packetId2.HasValue ) return OperationStatus.NeedMoreData;
                        await _store.OnQos1AckAsync( m, packetId2.Value, null );
                        return OperationStatus.Done;
                    case PacketType.PublishComplete:
                        ushort? packetId3 = await pipe.ReadPacketIdPacketAsync( m, pktLen, cancellationToken );
                        if( !packetId3.HasValue ) return OperationStatus.NeedMoreData;
                        _store.OnQos2AckStep2( m, packetId3.Value );
                        return OperationStatus.Done;
                    case PacketType.PublishRelease:
                        if( (header & 0b0010) != 2 ) throw new ProtocolViolationException( "MQTT-3.6.1-1 docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc384800427" );
                        ushort? packetId = await pipe.ReadPacketIdPacketAsync( m, pktLen, cancellationToken );
                        if( !packetId.HasValue ) return OperationStatus.NeedMoreData;
                        await _packetIdStore.RemoveIdAsync( m, packetId.Value );
                        // We doesn't care of the return value, if the queue is filled we have too much job to do currently and we drop that packet.
                        _output.QueueReflexMessage( m, LifecyclePacketV3.Pubcomp( packetId.Value ) );
                        return OperationStatus.Done;
                    case PacketType.PublishReceived:
                        ushort? packetId4 = await pipe.ReadPacketIdPacketAsync( m, pktLen, cancellationToken );
                        if( !packetId4.HasValue ) return OperationStatus.NeedMoreData;
                        IOutgoingPacket msg = await _store.OnQos2AckStep1Async( m, packetId4.Value );
                        _output.QueueReflexMessage( m, msg );
                        return OperationStatus.Done;
                    default:
                        return await next();
                }
            }
        }
    }
}
