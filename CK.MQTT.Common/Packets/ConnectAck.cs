using CK.Core;
using System;
using System.Buffers;
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

        public static ConnectAck? Deserialize( IActivityMonitor m, ReadOnlySequence<byte> buffer )
        {
            var reader = new SequenceParser<byte>( buffer );
            if(
                !reader.TryRead( out byte sessionState ) ||
                !reader.TryRead( out byte code )
            )
            {
                m.Error( "Malformed Packet: Not enoughs bytes in the ConnectAck packet." );
                return null;
            }
            if( reader.Remaining > 0 )
            {
                m.Warn( "Malformed Packet: Didn't read all the bytes in the ConnectAck packet." );
            }
            return new ConnectAck( (ConnectReturnCode)code, (SessionState)sessionState );
        }
    }
}
