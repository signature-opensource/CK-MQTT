using CK.MQTT.Client;
using CK.MQTT.Pumps;
using CK.MQTT.Server.OutgoingPackets;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server.Reflexes
{
    class PingReqReflex : IReflexMiddleware
    {
        readonly OutputPump _outputPump;

        public PingReqReflex( OutputPump outputPump )
        {
            _outputPump = outputPump;
        }
        public ValueTask<(OperationStatus, bool)> ProcessIncomingPacketAsync( IMqtt3Sink sink, InputPump sender, byte header, uint packetLength, PipeReader pipeReader, CancellationToken cancellationToken )
        {
            if( header >> 6 != 3 ) return new((OperationStatus.Done, false));
            _outputPump.TryQueueReflexMessage( OutgoingPingResp.Instance );
            return new( (OperationStatus.Done, true) );
        }
    }
}
