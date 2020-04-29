using CK.Core;
using System;
using System.Diagnostics;

namespace CK.MQTT.Common.Packets
{
    public class Disconnect : IPacket
    {

        public Disconnect()
        {
            ProtocoLevel = 4;
        }
        public byte ProtocoLevel { get; }

        public uint RemainingLength => 0;

        public byte HeaderByte => (byte)PacketType.Disconnect;

        public void Serialize( Span<byte> buffer )
        {
            Debug.Assert( buffer.Length == 0 );
        }

        public static Disconnect? Deserialize( IActivityMonitor m, ReadOnlySpan<byte> buffer )
        {
            if( buffer.Length > 0 )
            {
                m.Warn( "Malformed Packet: Packet length should be 0 in Disconnect packet." );
            }
            return new Disconnect();
        }

    }
}
