using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.MQTT
{
    public sealed class LifecyclePacketsV5 : SimpleOutgoingPacket, IOutgoingPacket
    {
        readonly byte _header;
        readonly ReasonCode _reason;
        readonly string _reasonString;
        readonly IReadOnlyList<UserProperty> _userProperties;
        readonly int _contentSize;
        readonly int _propertiesSize;
        public LifecyclePacketsV5( int packetId, byte header, ReasonCode reason, string reasonString, IReadOnlyList<UserProperty>? userProperties )
        {
            PacketId = packetId;
            _header = header;
            _reason = reason;
            _reasonString = reasonString;
            _userProperties = userProperties ?? Array.Empty<UserProperty>();
            _contentSize = 3;
            bool hasReason = !string.IsNullOrEmpty( reasonString );
            bool hasUserProperties = userProperties?.Count > 0;
            if( hasReason || hasUserProperties )
            {  // property: 1 byte + property content
                if( hasReason ) _propertiesSize += 1 + reasonString.MQTTSize();
                if( hasUserProperties ) _propertiesSize += userProperties.Sum( s => s.Size );
                _contentSize += _propertiesSize + _propertiesSize.CompactByteCount();
            }
            _getSize = GetSize( ProtocolLevel.MQTT5 ) + 1 + _contentSize.CompactByteCount() + _contentSize;
        }

        /// <inheritdoc/>
        public override int PacketId { get; set; }

        public override QualityOfService Qos => QualityOfService.AtLeastOnce;

        readonly int _getSize;

        /// <inheritdoc/>
        public override int GetSize( ProtocolLevel protocolLevel ) => _getSize;

        protected override void Write( ProtocolLevel protocolLevel, Span<byte> span )
        {
            bool hasReason = !string.IsNullOrEmpty( _reasonString );
            bool hasUserProperties = _userProperties.Count > 0;
            span[0] = _header;
            span = span[1..].WriteVariableByteInteger( _contentSize );
            BinaryPrimitives.WriteUInt16BigEndian( span, (ushort)PacketId );
            span = span[2..];
            span[0] = (byte)_reason;
            if( _propertiesSize == 0 )
            {
                Debug.Assert( span.Length == 0 );
                return;
            }
            span = span[1..].WriteVariableByteInteger( _propertiesSize );
            if( hasReason )
            {
                span[0] = (byte)PropertyIdentifier.ReasonString;
                span = span[1..].WriteMQTTString( _reasonString );
            }
            if( hasUserProperties )
            {
                foreach( UserProperty prop in _userProperties )
                {
                    span = prop.Write( span );
                }
            }
            Debug.Assert( span.Length == 0 );
        }

        public static IOutgoingPacket Pubrel( int packetId, ReasonCode reasonCode, string reasonString, IReadOnlyList<UserProperty>? userProperties )
            => new LifecyclePacketsV5( packetId, (byte)PacketType.PublishRelease | 0b0010, reasonCode, reasonString, userProperties );
        public static IOutgoingPacket Pubrec( int packetId, ReasonCode reasonCode, string reasonString, IReadOnlyList<UserProperty>? userProperties )
            => new LifecyclePacketsV5( packetId, (byte)PacketType.PublishReceived, reasonCode, reasonString, userProperties );
        public static IOutgoingPacket Puback( int packetId, ReasonCode reasonCode, string reasonString, IReadOnlyList<UserProperty>? userProperties )
           => new LifecyclePacketsV5( packetId, (byte)PacketType.PublishAck, reasonCode, reasonString, userProperties );
        public static IOutgoingPacket Pubcomp( int packetId, ReasonCode reasonCode, string reasonString, IReadOnlyList<UserProperty>? userProperties )
           => new LifecyclePacketsV5( packetId, (byte)PacketType.PublishComplete, reasonCode, reasonString, userProperties );
    }
}
