using CK.Core;
using CK.MQTT.Common.Serialisation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CK.MQTT.Common.Packets
{
    public class Unsubscribe : IPacket
    {
        readonly uint _size;
        public Unsubscribe( ushort packetId, IEnumerable<string> topics )
        {
            PacketId = packetId;
            Topics = topics;
            _size = (uint)(
                2 +
                Topics.Sum( s => Encoding.UTF8.GetByteCount( s ) ) +
                2 * Topics.Count());
        }

        //The bit set is caused by MQTT-3.8.1-1: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc442180829
        const byte _headerByte = (byte)PacketType.Unsubscribe | 0b0000_0010;
        public byte HeaderByte => _headerByte;

        public ushort PacketId { get; }

        public IEnumerable<string> Topics { get; }

        public uint RemainingLength => _size;

        public void Serialize( Span<byte> buffer )
        {
            buffer.WriteUInt16( PacketId );
            buffer = buffer[2..];
            foreach( string topic in Topics )
            {
                buffer = buffer.WriteString( topic );
            }
        }

        public static Unsubscribe? Deserialize( IActivityMonitor m, ReadOnlySpan<byte> buffer )
        {
            const string notEnoughBytes = "Malformed packet: Not enough bytes in the Unsubscribe packet.";
            if( buffer.Length < 4 )
            {
                //there should be a least 5 bytes: PacketId(2), topic filter length(2), and QoS(1)
                m.Error( notEnoughBytes );
                return null;
            }
            ushort packetId = buffer.ReadUInt16();
            buffer = buffer[2..];
            List<string> unsubs = new List<string>();
            do
            {
                buffer = buffer.ReadString( out string? unsub );
                if( unsub == null )
                {
                    m.Error( notEnoughBytes );
                    return null;
                }
                unsubs.Add( unsub );
            } while( buffer.Length > 0 );
            return new Unsubscribe( packetId, unsubs );
        }

    }
}
