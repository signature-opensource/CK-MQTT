using CK.Core;
using CK.MQTT.Common.Serialisation;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace CK.MQTT.Common.Packets
{
    public class PublishWithId : Publish, IPacketWithId
    {
        public PublishWithId(
            ApplicationMessage message, QualityOfService qualityOfService, bool retain, bool duplicated, ushort packetId )
            : base( message, retain, duplicated )
        {
            QualityOfService = qualityOfService;
            PacketId = packetId;
            _size += 2; //PackedId size.
        }
        public ushort PacketId { get; set; }

        public override QualityOfService QualityOfService { get; }

        public override void Serialize( Span<byte> buffer )
        {
            buffer = buffer.WriteString( Message.Topic );
            buffer.WriteUInt16( PacketId );
            buffer = buffer[2..].WritePayload( Message.Payload );
            Debug.Assert( buffer.Length == 0 );
        }
    }

    public class Publish : IPacket
    {
        protected const string _notEnoughBytes = "Malformed packet: Not enough bytes in the Publish packet.";

        protected uint _size;

        public Publish( ApplicationMessage message, bool retain, bool duplicated )
        {
            Message = message;
            Duplicated = duplicated;
            Retain = retain;
        }

        public virtual QualityOfService QualityOfService => QualityOfService.AtMostOnce;

        public bool Duplicated { get; set; }

        public bool Retain { get; }

        public ApplicationMessage Message { get; }

        protected const byte dupFlag = 1 << 4;
        protected const byte retainFlag = 1;
        public byte HeaderByte => (byte)
        (
            (byte)PacketType.Publish |
            (byte)(Duplicated ? dupFlag : 0) |
            (byte)QualityOfService << 1 |
            (byte)(Retain ? retainFlag : 0)
        );

        public uint RemainingLength => _size;

        public virtual void Serialize( Span<byte> buffer )
        {
            buffer = buffer.WriteString( Message.Topic )
                .WritePayload( Message.Payload );
            Debug.Assert( buffer.Length == 0 );
        }

        static QualityOfService QoSFromHeader( byte header )
            => (QualityOfService)((header << 5) >> 6);

        public static IPacket? Deserialise( IActivityMonitor m, byte header, ReadOnlyMemory<byte> memory )
        {
            QualityOfService qos = QoSFromHeader( header );
            if( qos == QualityOfService.AtMostOnce ) return DeserialiseQoS0( m, header, memory );
            if( qos == QualityOfService.AtLeastOnce || qos == QualityOfService.ExactlyOnce )
            {
                return DeserialiseWithQos1or2( m, header, memory );
            }
            m.Error( "Unknown QualityOfService." );
            return null;
        }

        static Publish? DeserialiseQoS0( IActivityMonitor m, byte header, ReadOnlyMemory<byte> memory )
        {
            if( memory.Length < 6 )
            {
                m.Error( _notEnoughBytes + "1" );
                return null;
            }
            memory = memory.ReadString( out string? topic );
            if( topic == null )
            {
                m.Error( "Topic string is bigger than the buffer or the max allowed string size." );
                return null;
            }
            if( memory.Length < 2 )
            {
                m.Error( _notEnoughBytes + "2" );
                return null;
            }
            memory = memory.ReadPayload( out ReadOnlyMemory<byte>? payload );
            if( !payload.HasValue )
            {
                m.Error( _notEnoughBytes + "3" );
                return null;
            }
            if( memory.Length > 0 )
            {
                m.Warn( "Malformed Packet: Unread bytes in the packets." );
                return null;
            }
            // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Ref383984666
            bool dup = (dupFlag & header) == retainFlag;
            QualityOfService qos = (QualityOfService)((header << 5) >> 6);
            Debug.Assert( qos == QualityOfService.AtMostOnce );
            bool retain = (retainFlag & header) == retainFlag;
            return new Publish( topic, payload.Value, retain, dup );
        }

        static PublishWithId? DeserialiseWithQos1or2( IActivityMonitor m, byte header, ReadOnlySequence<byte> buffer )
        {
            if( buffer.Length < 6 )
            {
                m.Error( _notEnoughBytes + "1" );
                return null;
            }
            SequenceReader<byte> reader = new SequenceReader<byte>( buffer );
            if( !reader.TryReadMQTTString( out string? topic ) )
            {
                m.Error( "Topic string is bigger than the buffer or the max allowed string size." );
                return null;
            }
            if( buffer.Length < 4 )
            {
                m.Error( _notEnoughBytes + "2" );
                return null;
            }
            ushort packetId = reader.ReadUInt16();
            if( !reader.TryReadMQTTPayload( out ReadOnlySequence<byte> payload ) )
            {
                m.Error( _notEnoughBytes + "3" );
                return null;
            }
            if( buffer.Length > 0 )
            {
                m.Warn( "Malformed Packet: Unread bytes in the packets." );
                return null;
            }
            // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Ref383984666
            bool dup = (dupFlag & header) == retainFlag;
            QualityOfService qos = (QualityOfService)((header << 5) >> 6);
            Debug.Assert( qos == QualityOfService.AtLeastOnce || qos == QualityOfService.ExactlyOnce );
            bool retain = (retainFlag & header) == retainFlag;
            return new PublishWithId( topic, payload, qos, retain, dup, packetId );
        }
    }
}
