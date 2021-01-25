using System;

namespace CK.MQTT
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

        /// <inheritdoc/>
        public override int GetSize( ProtocolLevel protocolLevel ) => 4;

        /// <inheritdoc/>
        protected override void Write( ProtocolLevel protocolLevel, Span<byte> span )
        {
            span[0] = (byte)PacketType.ConnectAck;
            span[1] = 2;
            span[2] = (byte)_sessionState;
            span[3] = (byte)_connectReturnCode;
        }
    }
}
