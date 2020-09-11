using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class PingRespReflex : IReflexMiddleware
    {
        public PingRespReflex( InputPump incomingMessageHandler )
        {
        }

        public bool WaitingPingResp { get; set; }

        public async ValueTask ProcessIncomingPacketAsync( IInputLogger? m, InputPump sender,
            byte header, int packetLength, PipeReader pipeReader, Func<ValueTask> next )
        {
            if( PacketType.PingResponse != (PacketType)header )
            {
                await next();
                return;
            }
            using( m?.ProcessPacket( PacketType.PingResponse ) )
            {
                WaitingPingResp = false;
                if( packetLength > 0 ) m?.UnparsedExtraBytes( sender, PacketType.PingResponse, 0, packetLength, packetLength );
                await pipeReader.BurnBytes( packetLength );
            }
        }
    }
}
