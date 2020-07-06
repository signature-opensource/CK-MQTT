using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class PublishLifecycleReflex : IReflexMiddleware
    {
        readonly PacketStore _store;
        readonly OutgoingMessageHandler _output;
        public PublishLifecycleReflex( PacketStore store, OutgoingMessageHandler output )
        {
            _store = store;
            _output = output;
        }

        public async ValueTask ProcessIncomingPacketAsync( IMqttLogger m, IncomingMessageHandler sender, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            PacketType packetType = (PacketType)((header >> 4) << 4);
            switch( packetType )
            {
                case PacketType.PublishAck:
                case PacketType.PublishComplete:
                    m.Trace( $"Handling incoming packet as {packetType}." );
                    ushort packetId3 = await pipeReader.ReadPacketIdPacket( m, packetLength );
                    await _store.DiscardPacketIdAsync( m, packetId3 );
                    return;
                case PacketType.PublishRelease:
                    if( (header & 0b0010) != 2 ) throw new ProtocolViolationException( "MQTT-3.6.1-1 docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc384800427" );
                    m.Trace( $"Handling incoming packet as {PacketType.PublishRelease}." );
                    ushort packetId = await pipeReader.ReadPacketIdPacket( m, packetLength );
                    await _store.DiscardPacketIdAsync( m, packetId );
                    _output.QueueReflexMessage( new OutgoingPubcomp( packetId ) );
                    return;
                case PacketType.PublishReceived:
                    m.Trace( $"Handling incoming packet as {PacketType.PublishReceived}." );
                    ushort packetId2 = await pipeReader.ReadPacketIdPacket( m, packetLength );
                    QualityOfService qos = await _store.DiscardMessageByIdAsync( m, packetId2 );
                    if( qos != QualityOfService.ExactlyOnce ) throw new ProtocolViolationException( $"This packet was stored with a qos '{qos}', but received a '{ PacketType.PublishReceived }' that should be sent in a protocol flow of QOS 2." );
                    _output.QueueReflexMessage( new OutgoingPubrel( packetId2 ) );
                    return;
                default:
                    await next();
                    return;
            }
        }
    }
}
