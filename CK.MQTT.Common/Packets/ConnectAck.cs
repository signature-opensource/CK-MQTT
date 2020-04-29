using CK.Core;
using System;
using System.Diagnostics;

namespace CK.MQTT.Common.Packets
{
    public class ConnectAck : IPacket
    {
        public ConnectAck( ConnectReturnCode status, SessionState existingSession )
        {
            ConnectReturnCode = status;
            SessionState = existingSession;
        }

        public ConnectReturnCode ConnectReturnCode { get; }

        public SessionState SessionState { get; }

        public uint RemainingLength => 2;

        public byte HeaderByte => (byte)PacketType.ConnectAck;

        public void Serialize( Span<byte> buffer )
        {
            Debug.Assert( buffer.Length == 2 );
            buffer[0] = (byte)SessionState;
            buffer[1] = (byte)ConnectReturnCode;
        }

        public static ConnectAck? Deserialize( IActivityMonitor m, ReadOnlySpan<byte> buffer )
        {
            if( buffer.Length < 2 )
            {
                m.Error( "Malformed Packet: Not enoughs bytes in the ConnectAck packet." );
                return null;
            }

            SessionState sessionState = (SessionState)buffer[0];
            ConnectReturnCode code = (ConnectReturnCode)buffer[1];
            if( buffer.Length > 2 )
            {
                m.Warn( "Malformed Packet: Didn't read all the bytes in the ConnectAck packet." );
            }
            return new ConnectAck( code, sessionState );
        }
    }
}
