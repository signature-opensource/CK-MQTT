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
    public class PubRelReflex : IReflexMiddleware
    {
        readonly IPacketStore _store;
        readonly OutgoingMessageHandler _output;

        public PubRelReflex( IPacketStore store, OutgoingMessageHandler output )
        {
            _store = store;
            _output = output;
        }
        public async ValueTask ProcessIncomingPacketAsync( IActivityMonitor m, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            if( PacketType.PublishRelease != (PacketType)header )
            {
                await next();
                return;
            }
            ushort packetId = await pipeReader.ReadUInt16();
            await _store.FreePacketIdAsync( m, packetId );
            _output.QueueMessage( new OutgoingPubcomp( packetId ) ); 
        }
    }
}
