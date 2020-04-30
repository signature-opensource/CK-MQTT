using CK.Core;
using System;
using System.Diagnostics;

namespace CK.MQTT.Common.Packets
{
    public class PingResponse : IPacket
    {
        public byte HeaderByte => (byte)PacketType.PingResponse;

        public uint RemainingLength => 0;

        public void Serialize( Span<byte> buffer )
        {
            Debug.Assert( buffer.Length == 0 );
        }

        public static PingResponse? Deserialize( IActivityMonitor m, ReadOnlySpan<byte> buffer )
        {
            if( buffer.Length > 0 )
            {
                m.Warn( "Malformed Packet: Unread bytes in the packets." );
            }
            return new PingResponse();
        }
    }
}
