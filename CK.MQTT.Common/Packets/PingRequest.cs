using CK.Core;
using System;
using System.Buffers;
using System.Diagnostics;

namespace CK.MQTT.Common.Packets
{
    public class PingRequest : IPacket
    {

        public byte HeaderByte => (byte)PacketType.PingRequest;

        public uint RemainingLength => 0;

        public void Serialize( Span<byte> buffer )
        {
            Debug.Assert( buffer.Length == 0 );
        }

        public static PingRequest? Deserialize( IActivityMonitor m, ReadOnlySequence<byte> buffer )
        {
            if( buffer.Length > 0 )
            {
                m.Warn( "Malformed Packet: Unread bytes in the packets." );
            }
            return new PingRequest();
        }
    }
}
