using CK.Core;
using CK.MQTT.Abstractions.Serialisation;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using CK.MQTT.Common.Stores;
using System;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Reflexes
{
    public class PubCompReflex : IReflexMiddleware
    {
        readonly PacketStore _store;
        readonly OutgoingMessageHandler _output;

        public PubCompReflex( PacketStore store, OutgoingMessageHandler output )
        {
            _store = store;
            _output = output;
        }

        public async ValueTask ProcessIncomingPacketAsync( IActivityMonitor m, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            if( PacketType.PublishComplete != (PacketType)header )
            {
                await next();
                return;
            }
            ushort packetId = await pipeReader.ReadUInt16();
            await _store.DiscardPacketIdAsync( m, packetId );
        }
    }
}