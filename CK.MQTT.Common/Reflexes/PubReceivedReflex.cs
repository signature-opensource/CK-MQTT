using CK.Core;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.OutgoingPackets;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using CK.MQTT.Common.Stores;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Reflexes
{
    public class PubReceivedReflex : IReflexMiddleware
    {
        readonly IPacketStore _store;
        readonly OutgoingMessageHandler _output;

        public PubReceivedReflex( IPacketStore store, OutgoingMessageHandler output )
        {
            _store = store;
            _output = output;
        }
        public async ValueTask ProcessIncomingPacketAsync( IActivityMonitor m, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            if( PacketType.PublishReceived != (PacketType)header )
            {
                await next();
                return;
            }
            ushort packetId = await pipeReader.ReadUInt16();
            QualityOfService qos = await _store.DiscardMessageByIdAsync( m, packetId );
            if( qos != QualityOfService.ExactlyOnce )
            {
                throw new ProtocolViolationException( $"This packet was stored with a qos '{qos}'," +
                    $"but received a '{ PacketType.PublishReceived }' that should be sent in a protocol flow of QOS 2." );
            }
            _output.QueueMessage( new OutgoingPubrel( packetId ) );
        }
    }
}
