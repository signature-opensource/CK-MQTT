using CK.Core;
using CK.MQTT.Client.Deserialization;
using CK.MQTT.Common;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Stores;
using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading.Tasks;

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
            IActivityMonitor m, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            if( PacketType.SubscribeAck != (PacketType)header )
            {
                await next();
                return;
            }
            ushort packetId;
            QualityOfService[]? qos;
            SequencePosition position;
            while( true )
            {
                ReadResult read = await pipeReader.ReadAsync();
                if( read.IsCanceled ) return;
                if( Suback.TryParse( read.Buffer, packetLength, out packetId, out qos, out position ) )
                {
                    break;
                }
                pipeReader.AdvanceTo( read.Buffer.Start, read.Buffer.End );
            };
            pipeReader.AdvanceTo( position );
            QualityOfService debugQos = await _store.DiscardMessageByIdAsync( m, packetId, qos );
            Debug.Assert( debugQos == QualityOfService.AtLeastOnce );
        }
    }
}
