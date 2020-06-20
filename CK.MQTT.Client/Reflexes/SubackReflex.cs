using CK.Core;
using CK.MQTT.Client.Deserialization;
using CK.MQTT.Common;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Stores;
using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading.Tasks;
using CK.MQTT.Abstractions.Serialisation;

namespace CK.MQTT.Client.Reflexes
{
    class SubackReflex : IReflexMiddleware
    {
        readonly PacketStore _store;

        public SubackReflex( PacketStore store )
        {
            _store = store;
        }
        public async ValueTask ProcessIncomingPacketAsync(
            IActivityMonitor m, IncomingMessageHandler sender, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            if( PacketType.SubscribeAck != (PacketType)header )
            {
                await next();
                return;
            }
            m.Trace( $"Handling incoming packet as {PacketType.SubscribeAck}." );
            ReadResult? read = await pipeReader.ReadAsync( m, packetLength );
            if( !read.HasValue ) return;
            bool result = Suback.TryParse( read.Value.Buffer, packetLength, out ushort packetId, out QualityOfService[]? qos, out SequencePosition position );
            Debug.Assert( result );
            pipeReader.AdvanceTo( position );
            QualityOfService debugQos = await _store.DiscardMessageByIdAsync( m, packetId, qos );
            Debug.Assert( debugQos == QualityOfService.AtLeastOnce );
        }
    }
}
