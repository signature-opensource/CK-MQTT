using CK.Core;
using CK.MQTT.Common.Serialisation;
using System;
using System.Diagnostics;

namespace CK.MQTT.Common.Packets
{
    public class PublishReceived : IPacketWithId
    {
        public PublishReceived( ushort packetId )
        {
            PacketId = packetId;
        }

        public ushort PacketId { get; }

        public byte HeaderByte => (byte)PacketType.PublishReceived;

        public uint RemainingLength => 2;

        public void Serialize( Span<byte> buffer )
        {
            Debug.Assert( buffer.Length == 2 );
            buffer.WriteUInt16( PacketId );
        }

        public static PublishReceived? Deserialize( IActivityMonitor m, ReadOnlySpan<byte> buffer )
        {
            if( buffer.Length < 2 )
            {
                m.Error( "Malformed Packet: Packet too small." );
                return null;
            }
            ushort packetId = buffer.ReadUInt16();
            if( buffer.Length > 2 )
            {
                m.Warn( "Malformed Packet: Unread bytes in the packets." );
            }
            return new PublishReceived( packetId );
        }
    }
}
