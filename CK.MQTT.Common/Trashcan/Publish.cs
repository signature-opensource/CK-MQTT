//using CK.Core;
//using CK.MQTT.Common.Serialisation;
//using System;
//using System.Buffers;
//using System.Diagnostics;
//using System.Text;

//namespace CK.MQTT.Common.Packets
//{
//    public class PublishWithId : Publish, IPacketWithId
//    {
//        public PublishWithId(
//            OutgoingApplicationMessage message, QualityOfService qualityOfService, bool retain, bool duplicated, ushort packetId )
//            : base( message, retain, duplicated )
//        {
//            QualityOfService = qualityOfService;
//            PacketId = packetId;
//            _size += 2; //PackedId size.
//        }
//        public ushort PacketId { get; set; }

//        public override QualityOfService QualityOfService { get; }

//        public override void Serialize( Span<byte> buffer )
//        {
//            buffer = buffer.WriteString( Message.Topic );
//            buffer.WriteUInt16( PacketId );
//            buffer = buffer[2..].WritePayload( Message.Payload );
//            Debug.Assert( buffer.Length == 0 );
//        }
//    }

//    public class Publish : IPacket
//    {
//        protected const string _notEnoughBytes = "Malformed packet: Not enough bytes in the Publish packet.";

//        protected uint _size;

//        public Publish( OutgoingApplicationMessage message, bool retain, bool duplicated )
//        {
//            Message = message;
//            Duplicated = duplicated;
//            Retain = retain;
//        }

//        public virtual QualityOfService QualityOfService => QualityOfService.AtMostOnce;

//        public bool Duplicated { get; set; }

//        public bool Retain { get; }

//        public OutgoingApplicationMessage Message { get; }

//        
//        public byte HeaderByte => (byte)
//        (
//            (byte)PacketType.Publish |
//            (byte)(Duplicated ? dupFlag : 0) |
//            (byte)QualityOfService << 1 |
//            (byte)(Retain ? retainFlag : 0)
//        );

//        public uint RemainingLength => _size;

//        public virtual void Serialize( Span<byte> buffer )
//        {
//            buffer = buffer.WriteString( Message.Topic )
//                .WritePayload( Message.Payload );
//            Debug.Assert( buffer.Length == 0 );
//        }


static Publish? DeserialiseQoS0( IActivityMonitor m, byte header, ReadOnlySequence<byte> sequence )
{
    SequenceReader<byte> reader = new SequenceReader<byte>( sequence );
    if( !reader.TryReadMQTTString( out string? topic )
     || !reader.TryReadMQTTPayload( out ReadOnlySequence<byte> payload ) )
    {
        m.Error( _notEnoughBytes );
        return null;
    }
    if( reader.Remaining > 0 )
    {
        m.Warn( "Malformed Packet: Unread bytes in the packets." );
        return null;
    }
    // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Ref383984666
    bool dup = (dupFlag & header) == retainFlag;
    QualityOfService qos = (QualityOfService)((header << 5) >> 6);
    Debug.Assert( qos == QualityOfService.AtMostOnce );
    bool retain = (retainFlag & header) == retainFlag;
    return new Publish( new OutgoingApplicationMessage( topic, payload ), retain, dup );
}

//        static PublishWithId? DeserialiseWithQos1or2( IActivityMonitor m, byte header, ReadOnlySequence<byte> sequence )
//        {
//            SequenceReader<byte> reader = new SequenceReader<byte>( sequence );
//            if( !reader.TryReadMQTTString( out string? topic )
//             || !reader.TryReadBigEndian( out ushort packetId )
//             || !reader.TryReadMQTTPayload( out ReadOnlySequence<byte> payload ) )
//            {
//                m.Error( "Topic string is bigger than the buffer or the max allowed string size." );
//                return null;
//            }
//            if( sequence.Length > 0 )
//            {
//                m.Warn( "Malformed Packet: Unread bytes in the packets." );
//                return null;
//            }
//            // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Ref383984666
//            bool dup = (dupFlag & header) == retainFlag;
//            QualityOfService qos = (QualityOfService)((header << 5) >> 6);
//            Debug.Assert( qos == QualityOfService.AtLeastOnce || qos == QualityOfService.ExactlyOnce );
//            bool retain = (retainFlag & header) == retainFlag;
//            return new PublishWithId( new OutgoingApplicationMessage( topic, payload ), qos, retain, dup, packetId );
//        }
//    }
//}
