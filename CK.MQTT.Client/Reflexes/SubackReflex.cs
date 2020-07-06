using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class SubackReflex : IReflexMiddleware
    {
        readonly PacketStore _store;

        public SubackReflex( PacketStore store )
        {
            _store = store;
        }
        public async ValueTask ProcessIncomingPacketAsync(
            IMqttLogger m, IncomingMessageHandler sender, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
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
