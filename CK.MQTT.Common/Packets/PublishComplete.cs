using CK.Core;
using CK.MQTT.Common.Serialisation;
using System;
using System.Diagnostics;

namespace CK.MQTT.Common.Packets
{
    public class PublishComplete : IPacketWithId
    {
        public PublishComplete( ushort packetId )
        {
            PacketId = packetId;
        }

        public ushort PacketId { get; }

        public byte HeaderByte => (byte)PacketType.PublishComplete;

        public uint RemainingLength => 2;

        public void Serialize( Span<byte> buffer )
        {
            Debug.Assert( buffer.Length == 2 );
            buffer.WriteUInt16( PacketId );
        }

        public static PublishComplete? Deserialize( IActivityMonitor m, ReadOnlySpan<byte> buffer )
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
            return new PublishComplete( packetId );
        }
    }
}
