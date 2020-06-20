using CK.MQTT.Common;
using CK.MQTT.Common.Packets;
using System;

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

        public override int GetSize() => 4;

        protected override void Write( Span<byte> span )
        {
            span[0] = (byte)PacketType.ConnectAck;
            span[1] = 2;
            span[2] = (byte)_sessionState;
            span[3] = (byte)_connectReturnCode;
        }
    }
}
