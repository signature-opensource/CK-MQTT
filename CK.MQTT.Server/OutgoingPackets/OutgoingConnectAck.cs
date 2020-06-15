using CK.MQTT.Common;
using CK.MQTT.Common.Packets;
using System;
using System.IO.Pipelines;

namespace CK.MQTT.Server
{
    public class OutgoingConnectAck : SimpleOutgoingPacket
    {
        readonly ConnectReturnCode _connectReturnCode;
        readonly SessionState _sessionState;
        public OutgoingConnectAck( SessionState existingSession, ConnectReturnCode status )
        {
            _connectReturnCode = status;
            _sessionState = existingSession;
        }

        protected override PacketType PacketType => PacketType.ConnectAck;

        protected override void Write( PipeWriter pw )
        {
            Span<byte> buffer = pw.GetSpan( 4 );
            buffer[0] = (byte)PacketType;
            buffer[1] = 2;
            buffer[2] = (byte)_sessionState;
            buffer[3] = (byte)_connectReturnCode;
            pw.Advance( 4 );
        }
    }
}
