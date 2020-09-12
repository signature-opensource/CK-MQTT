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
        readonly PacketStore _store;
        readonly OutputPump _output;
        public PublishLifecycleReflex( IPacketIdStore packetIdStore, PacketStore store, OutputPump output )
        {
            _packetIdStore = packetIdStore;
            _store = store;
            _output = output;
        }

        public async ValueTask ProcessIncomingPacketAsync( IInputLogger? m, InputPump sender, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next, CancellationToken cancellationToken )
        {
            PacketType packetType = (PacketType)((header >> 4) << 4);//to remove right bits that may store flags data
            switch( packetType )
            {
                case PacketType.PublishAck:
                case PacketType.PublishComplete:
                    m?.ProcessPacket( packetType );
                    ushort packetId3 = await pipeReader.ReadPacketIdPacket( m, packetLength );
                    await _store.DiscardPacketIdAsync( m, packetId3 );
                    return;
                case PacketType.PublishRelease:
                    if( (header & 0b0010) != 2 ) throw new ProtocolViolationException( "MQTT-3.6.1-1 docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc384800427" );
                    m?.ProcessPacket( PacketType.PublishRelease );
                    ushort packetId = await pipeReader.ReadPacketIdPacket( m, packetLength );
                    await _packetIdStore.RemoveId( m, packetId );
                    _output.QueueReflexMessage( LifecyclePacketV3.Pubcomp( packetId ) );
                    return;
                case PacketType.PublishReceived:
                    m?.ProcessPacket( PacketType.PublishReceived );
                    ushort packetId2 = await pipeReader.ReadPacketIdPacket( m, packetLength );
                    QualityOfService qos = await _store.DiscardMessageByIdAsync( m, packetId2 );
                    if( qos != QualityOfService.ExactlyOnce ) throw new ProtocolViolationException( $"This packet was stored with a qos '{qos}', but received a '{ PacketType.PublishReceived }' that should be sent in a protocol flow of QOS 2." );
                    _output.QueueReflexMessage( LifecyclePacketV3.Pubrel( packetId2 ) );
                    return;
                default:
                    await next();
                    return;
            }
        }
    }
}
