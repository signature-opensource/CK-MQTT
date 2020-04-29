using CK.Core;
using CK.MQTT.Common.Serialisation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CK.MQTT.Common.Packets
{
    public class Subscribe : IPacketWithId
    {
        readonly uint _size;
        public Subscribe( ushort packetId, params Subscription[] subscriptions )
        {
            Debug.Assert( subscriptions.Length > 0 );
            PacketId = packetId;
            Subscriptions = subscriptions;
            _size = (uint)(
                2 +
                subscriptions.Sum( s => Encoding.UTF8.GetByteCount( s.TopicFilter ) ) +
                3 * subscriptions.Length);
        }
        //The bit set is caused by MQTT-3.8.1-1: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc442180829
        const byte _headerByte = (byte)PacketType.Subscribe | 0b0000_0010;

        public byte HeaderByte => _headerByte;

        public ushort PacketId { get; }

        public IEnumerable<Subscription> Subscriptions { get; }


        public uint RemainingLength => _size;

        public void Serialize( Span<byte> buffer )
        {
            buffer.WriteUInt16( PacketId );
            buffer = buffer[2..];
            foreach( Subscription sub in Subscriptions )
            {
                buffer = buffer.WriteString( sub.TopicFilter );
                buffer[0] = (byte)sub.MaximumQualityOfService;
                buffer = buffer[1..];
            }
        }

        public static Subscribe? Deserialize( IActivityMonitor m, ReadOnlySpan<byte> buffer )
        {
            const string notEnoughBytes = "Malformed packet: Not enough bytes in the Subscribe packet.";
            if( buffer.Length < 5 )
            {
                //there should be a least 5 bytes: PacketId(2), topic filter length(2), and QoS(1)
                m.Error( notEnoughBytes );
                return null;
            }
            ushort packetId = buffer.ReadUInt16();
            buffer = buffer[2..];
            List<Subscription> subscriptions = new List<Subscription>();
            do
            {
                buffer = buffer.ReadString( out string? topicFilter );
                if( topicFilter == null )
                {
                    m.Error( notEnoughBytes );
                    return null;
                }
                QualityOfService qos = (QualityOfService)buffer[0];
                buffer = buffer[1..];
                subscriptions.Add( new Subscription( topicFilter, qos ) );
            } while( buffer.Length > 0 );
            return new Subscribe( packetId, subscriptions.ToArray() );
        }
    }
}
