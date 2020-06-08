//using CK.Core;
//using CK.MQTT.Common.Serialisation;
//using System;
//using System.Buffers;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;

//namespace CK.MQTT.Common.Packets
//{
//    public class Subscribe : IPacketWithId
//    {
//        readonly uint _size;
//        public Subscribe( ushort packetId, params Subscription[] subscriptions )
//        {
//            Debug.Assert( subscriptions.Length > 0 );
//            PacketId = packetId;
//            Subscriptions = subscriptions;
//            _size = (uint)(
//                2 +
//                subscriptions.Sum( s => Encoding.UTF8.GetByteCount( s.TopicFilter ) ) +
//                3 * subscriptions.Length);
//        }


//        public byte HeaderByte => _headerByte;

//        public ushort PacketId { get; }

//        public IEnumerable<Subscription> Subscriptions { get; }


//        public uint RemainingLength => _size;

//        public void Serialize( Span<byte> buffer )
//        {
//            buffer.WriteUInt16( PacketId );
//            buffer = buffer[2..];
//            foreach( Subscription sub in Subscriptions )
//            {
//                buffer = buffer.WriteString( sub.TopicFilter );
//                buffer[0] = (byte)sub.MaximumQualityOfService;
//                buffer = buffer[1..];
//            }
//        }

//        public static Subscribe? Deserialize( IActivityMonitor m, ReadOnlySequence<byte> buffer )
//        {
//            SequenceReader<byte> reader = new SequenceReader<byte>( buffer );
//            const string notEnoughBytes = "Malformed packet: Not enough bytes in the Subscribe packet.";
//            if( !reader.TryReadBigEndian( out ushort packetId ) )
//            {
//                //there should be a least 5 bytes: PacketId(2), topic filter length(2), and QoS(1)
//                m.Error( notEnoughBytes );
//                return null;
//            }
//            List<Subscription> subscriptions = new List<Subscription>();
//            do
//            {
//                if( !reader.TryReadMQTTString( out string? topicFilter )
//                 || !reader.TryRead( out byte qosByte ) )
//                {
//                    m.Error( notEnoughBytes );
//                    return null;
//                }

//                QualityOfService qos = (QualityOfService)qosByte;
//                subscriptions.Add( new Subscription( topicFilter, qos ) );
//            } while( buffer.Length > 0 );
//            return new Subscribe( packetId, subscriptions.ToArray() );
//        }
//    }
//}
