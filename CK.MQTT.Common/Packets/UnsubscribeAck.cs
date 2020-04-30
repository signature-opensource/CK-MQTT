using CK.Core;
using CK.MQTT.Common.Serialisation;
using System;

namespace CK.MQTT.Common.Packets
{
    public class UnsubscribeAck : IPacket
    {
        public UnsubscribeAck( ushort packetId )
        {
            PacketId = packetId;
        }

        public ushort PacketId { get; }

        public byte HeaderByte => (byte)PacketType.UnsubscribeAck;

        public uint RemainingLength => 2;

        public void Serialize( Span<byte> stream )
        {
            stream.WriteUInt16( PacketId );
        }

        public static UnsubscribeAck? Deserialize( IActivityMonitor m, ReadOnlySpan<byte> buffer )
        {
            if( buffer.Length < 2 )
            {
                m.Error( "Malformed Packet: UnsubscribeAck packet dont get enough bytes" );
                return null;
            }
            ushort packetId = buffer.ReadUInt16();
            if( buffer.Length > 2 )
            {
                m.Warn( "Malformed Packet: Did not read all the bytes of the UnsubscribeAck packet." );
            }
            return new UnsubscribeAck( packetId );
        }
    }
}
