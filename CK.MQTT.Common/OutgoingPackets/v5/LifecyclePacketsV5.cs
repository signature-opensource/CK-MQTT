using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CK.MQTT.Common.OutgoingPackets.v5
{
    public sealed class LifecyclePacketsV5 : SimpleOutgoingPacket, IOutgoingPacketWithId
    {
        readonly byte _header;
        readonly ReasonCode _reason;
        readonly string _reasonString;
        readonly IReadOnlyList<(string, string)>? _userProperties;
        readonly int _contentSize;
        readonly int _propertySize;
        public LifecyclePacketsV5( int packetId, byte header, ReasonCode reason, string reasonString, IReadOnlyList<(string, string)>? userProperties )
        {
            PacketId = packetId;
            _header = header;
            _reason = reason;
            _reasonString = reasonString;
            _userProperties = userProperties;
            _contentSize = 3;
            bool hasReason = !string.IsNullOrEmpty( reasonString );
            bool hasUserProperties = (userProperties?.Count ?? 0) > 0;
            if( hasReason || hasUserProperties )
            {  // property: 1 byte + property content
                if( hasReason ) _propertySize += 1 + reasonString.MQTTSize();
                if( hasUserProperties ) _propertySize += userProperties.Select( s => 1 + s.Item1.MQTTSize() + s.Item2.MQTTSize() ).Sum();
                _contentSize += _propertySize + _propertySize.CompactByteCount();
            }
            Size += 1 + _contentSize.CompactByteCount() + _contentSize;
        }

        /// <inheritdoc/>
        public int PacketId { get; set; }

        public QualityOfService Qos => QualityOfService.AtLeastOnce;

        /// <inheritdoc/>
        public override int Size { get; }
        protected override void Write( Span<byte> span )
        {
            bool hasReason = !string.IsNullOrEmpty( _reasonString );
            bool hasUserProperties = (_userProperties?.Count ?? 0) > 0;
            span[0] = _header;
            if( !hasReason && !hasUserProperties )
            {
                span[1] = 2;
                span = span[2..].WriteUInt16( (ushort)PacketId );
                span[0] = (byte)_reason;
                Debug.Assert( Size == 5 );
            }
            else
            {
                span = span[1..].WriteVariableByteInteger( _contentSize );
                span = span.WriteUInt16( (ushort)PacketId );
                span[0] = (byte)_reason;
                span = span[1..].WriteVariableByteInteger( _propertySize );
                if( hasReason )
                {
                    span[0] = (byte)PropertyIdentifier.ReasonString;
                    span = span[1..].WriteMQTTString( _reasonString );
                }
                if( hasUserProperties )
                {
                    foreach( (string, string) prop in _userProperties! )
                    {
                        span[0] = (byte)PropertyIdentifier.UserProperty;
                        span = span[1..]
                            .WriteMQTTString( prop.Item1 )
                            .WriteMQTTString( prop.Item2 );
                    }
                }
                Debug.Assert( span.Length == 0 );
            }
        }

        public static IOutgoingPacket Pubrel( int packetId, ReasonCode reasonCode, string reasonString, IReadOnlyList<(string, string)>? userProperties )
            => new LifecyclePacketsV5( packetId, (byte)PacketType.PublishRelease | 0b0010, reasonCode, reasonString, userProperties );

        public static IOutgoingPacket Pubrec( int packetId, ReasonCode reasonCode, string reasonString, IReadOnlyList<(string, string)>? userProperties )
            => new LifecyclePacketsV5( packetId, (byte)PacketType.PublishReceived, reasonCode, reasonString, userProperties );
        public static IOutgoingPacket Puback( int packetId, ReasonCode reasonCode, string reasonString, IReadOnlyList<(string, string)>? userProperties )
           => new LifecyclePacketsV5( packetId, (byte)PacketType.PublishAck, reasonCode, reasonString, userProperties );
        public static IOutgoingPacket Pubcomp( int packetId, ReasonCode reasonCode, string reasonString, IReadOnlyList<(string, string)>? userProperties )
           => new LifecyclePacketsV5( packetId, (byte)PacketType.PublishComplete, reasonCode, reasonString, userProperties );
    }
}
