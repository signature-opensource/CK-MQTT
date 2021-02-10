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
        readonly IPacketIdStore _packetIdStore;
        readonly IMqttIdStore _store;
        readonly OutputPump _output;

        public PublishLifecycleReflex( IPacketIdStore packetIdStore, IMqttIdStore store, OutputPump output )
            => (_packetIdStore, _store, _output) = (packetIdStore, store, output);

        public async ValueTask ProcessIncomingPacketAsync(
            IInputLogger? m, InputPump input, byte header, int pktLen, PipeReader pipe, Func<ValueTask> next, CancellationToken cancellationToken )
        {
            PacketType packetType = (PacketType)((header >> 4) << 4);//to remove right bits that may store flags data
            switch( packetType )
            {
                case PacketType.PublishAck:
                case PacketType.PublishComplete:
                    m?.ProcessPacket( packetType );
                    ushort packetId3 = await pipe.ReadPacketIdPacket( m, pktLen );
                    _store.OnQos2AckStep2( m, packetId3 );
                    return;
                case PacketType.PublishRelease:
                    if( (header & 0b0010) != 2 ) throw new ProtocolViolationException( "MQTT-3.6.1-1 docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc384800427" );
                    m?.ProcessPacket( PacketType.PublishRelease );
                    ushort packetId = await pipe.ReadPacketIdPacket( m, pktLen );
                    await _packetIdStore.RemoveId( m, packetId );
                    _output.QueueReflexMessage( LifecyclePacketV3.Pubcomp( packetId ) );
                    return;
                case PacketType.PublishReceived:
                    m?.ProcessPacket( PacketType.PublishReceived );
                    ushort packetId2 = await pipe.ReadPacketIdPacket( m, pktLen );
                    await _store.OnQos2AckStep1Async( m, packetId2 );
                    _output.QueueReflexMessage( LifecyclePacketV3.Pubrel( packetId2 ) );
                    return;
                default:
                    await next();
                    return;
            }
        }
    }
}
