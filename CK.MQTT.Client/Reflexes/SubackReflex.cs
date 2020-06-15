using CK.Core;
using CK.MQTT.Client.Deserialization;
using CK.MQTT.Common;
using CK.MQTT.Common.Packets;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Reflexes
{
    class SubackReflex : IReflexMiddleware
    {
        readonly Action<ushort, QualityOfService[]> _callback;

        public SubackReflex(Action<ushort, QualityOfService[]> callback)
        {
            _callback = callback;
        }
        public async ValueTask ProcessIncomingPacketAsync( IActivityMonitor m, byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            if( PacketType.SubscribeAck != (PacketType)header )
            {
                await next();
                return;
            }
            Parse:
            ReadResult read = await pipeReader.ReadAsync();
            if( read.IsCanceled ) return;
            if( !Suback.TryParse( read.Buffer, packetLength, out ushort packetId, out QualityOfService[]? qos, out SequencePosition position ) )
            {
                pipeReader.AdvanceTo( read.Buffer.Start, read.Buffer.End );
                goto Parse;
            }
            pipeReader.AdvanceTo( position );
            _callback( packetId, qos );
        }
    }
}
