using CK.Core;
using CK.MQTT.Abstractions.Serialisation;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.OutgoingPackets;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Stores;
using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Reflexes
{
    public class PubRelReflex : IReflexMiddleware
    {
        readonly PacketStore _store;
        readonly OutgoingMessageHandler _output;

        public PubRelReflex( PacketStore store, OutgoingMessageHandler output )
        {
            _store = store;
            _output = output;
        }
        public async ValueTask ProcessIncomingPacketAsync( IActivityMonitor m, IncomingMessageHandler sender,
            byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            if( ((byte)PacketType.PublishRelease | 0b0010) != header  )
            {
                await next();
                return;
            }
            m.Trace( $"Handling incoming packet as {PacketType.PublishRelease}." );
            ushort packetId = await pipeReader.ReadPacketIdPacket( m, packetLength );
            await _store.DiscardPacketIdAsync( m, packetId );
            _output.QueueReflexMessage( new OutgoingPubcomp( packetId ) );
        }
    }
}
