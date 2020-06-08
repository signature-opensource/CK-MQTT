using CK.Core;
using CK.MQTT.Common.Serialisation;
using System;
using System.Buffers;
using System.Diagnostics;

namespace CK.MQTT.Common.Packets
{
    public class PublishRelease : IPacketWithId
    {
        public PublishRelease( ushort packetId )
        {
            PacketId = packetId;
        }

        public ushort PacketId { get; }
        /// <summary>
        /// //MQTT-3.6.1-1: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc442180829;
        /// </summary>
        const byte headerByte = (byte)PacketType.PublishRelease | 0b0000_0010;
        public byte HeaderByte => headerByte;

        public uint RemainingLength => 2;

        public void Serialize( Span<byte> buffer )
        {
            Debug.Assert( buffer.Length == 2 );
            buffer.WriteUInt16( PacketId );
        }

        public static PublishRelease? Deserialize( IActivityMonitor m, byte header, ReadOnlySpan<byte> buffer )
        {
            var reader = new SequenceReader<byte>();
            if( header != headerByte )
            {
                m.Error( $"Malformed Packet: expected {headerByte}." ); //http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349292
                return null;
            }
            if( !reader.TryReadBigEndian( out ushort packetId ) )
            {
                m.Error( "Malformed Packet: Packet too small." );
                return null;
            }
            if( buffer.Length > 2 )
            {
                m.Warn( "Malformed Packet: Unread bytes in the packets." );
            }
            return new PublishRelease( packetId );
        }
    }
}
