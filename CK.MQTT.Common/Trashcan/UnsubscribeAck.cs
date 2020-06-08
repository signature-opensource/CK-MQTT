using CK.Core;
using CK.MQTT.Common.Serialisation;
using System;
using System.Buffers;

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

        public static UnsubscribeAck? Deserialize( IActivityMonitor m, ReadOnlySequence<byte> buffer )
        {
            SequenceReader<byte> reader = new SequenceReader<byte>( buffer );
            if( !reader.TryReadBigEndian( out ushort packetId ) )
            {
                m.Error( "Malformed Packet: UnsubscribeAck packet dont get enough bytes" );
                return null;
            }
            if( reader.Remaining > 2 )
            {
                m.Warn( "Malformed Packet: Did not read all the bytes of the UnsubscribeAck packet." );
            }
            return new UnsubscribeAck( packetId );
        }
    }
}
