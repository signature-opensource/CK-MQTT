using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.IO.Pipelines;
using System.Net;
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

        public async ValueTask ProcessIncomingPacketAsync(
            IInputLogger? m, InputPump input, byte header, int pktLen, PipeReader pipe, Func<ValueTask> next, CancellationToken cancellationToken )
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
                    await next();
                    return;
            }
            using( m?.ProcessPacket( packetType ) )
            {
                switch( packetType )
                {
                    case PacketType.PublishAck:
                        ushort packetId2 = await pipe.ReadPacketIdPacketAsync( m, pktLen );
                        await _store.OnQos1AckAsync( m, packetId2, null );
                        return;
                    case PacketType.PublishComplete:
                        ushort packetId3 = await pipe.ReadPacketIdPacketAsync( m, pktLen );
                        _store.OnQos2AckStep2( m, packetId3 );
                        return;
                    case PacketType.PublishRelease:
                        if( (header & 0b0010) != 2 ) throw new ProtocolViolationException( "MQTT-3.6.1-1 docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc384800427" );
                        ushort packetId = await pipe.ReadPacketIdPacketAsync( m, pktLen );
                        await _packetIdStore.RemoveIdAsync( m, packetId );
                        // We doesn't care of the return value, if the queue is filled we have too much job to do currently and we drop that packet.
                        _= _output.QueueReflexMessage( LifecyclePacketV3.Pubcomp( packetId ) );
                        return;
                    case PacketType.PublishReceived:
                        ushort packetId4 = await pipe.ReadPacketIdPacketAsync( m, pktLen );
                        IOutgoingPacket msg = await _store.OnQos2AckStep1Async( m, packetId4 );
                        _output.QueueReflexMessage( msg );
                        return;
                    default:
                        await next();
                        return;
                }
            }
        }
    }
}
