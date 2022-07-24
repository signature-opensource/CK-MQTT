using CK.MQTT.Client;
using CK.MQTT.Pumps;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class Reflex
    {
        readonly ReadOnlyMemory<IReflexMiddleware> _middlewares;

        public Reflex( ReadOnlyMemory<IReflexMiddleware> middlewares )
        {
            _middlewares = middlewares;
        }

        public async ValueTask<OperationStatus> ProcessIncomingPacketAsync( IMQTT3Sink sink, InputPump sender, byte header, uint packetLength, PipeReader pipeReader, CancellationToken cancellationToken )
        {
            for( int i = 0; i < _middlewares.Length; i++ )
            {
                (OperationStatus status, bool processed) = await _middlewares.Span[i].ProcessIncomingPacketAsync( sink, sender, header, packetLength, pipeReader, cancellationToken );
                if( processed ) return status;
            }
            // No reflex matched the packet.
            await sender.SelfCloseAsync( DisconnectReason.ProtocolError );
            return OperationStatus.Done;
        }

    }
}
