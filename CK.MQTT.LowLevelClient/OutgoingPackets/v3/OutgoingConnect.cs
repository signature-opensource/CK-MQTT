using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Packets;

class OutgoingConnect : ComplexOutgoingPacket
{
    readonly ProtocolConfiguration _pConfig;
    readonly MQTT3ClientConfiguration _config;
    readonly OutgoingLastWill? _lastWill;
    readonly uint _sessionExpiryInterval;
    readonly ushort _receiveMaximum;
    readonly ushort _topicAliasMaximum;
    readonly bool _requestResponseInformation;
    readonly bool _requestProblemInformation;
    readonly IReadOnlyList<UserProperty>? _userProperties;
    readonly (string authMethod, ReadOnlyMemory<byte> authData)? _extendedAuth;
    readonly byte _flags;
    readonly uint _sizePostPayload;
    readonly uint _propertiesSize;
    static byte ByteFlag( string? username, string? password, bool cleanSession, OutgoingLastWill? lastWill )
    {
        byte flags = 0;
        if( username != null ) flags |= 0b1000_0000; // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Figure_3.4_-
        if( password != null ) flags |= 0b0100_0000;
        if( lastWill?.Retain ?? false ) flags |= 0b0010_0000;
        flags |= (byte)((byte)(lastWill?.Qos ?? 0) << 3);
        if( lastWill != null ) flags |= 0b0000_0100;
        if( cleanSession ) flags |= 0b0000_0010;
        return flags;
    }

    public OutgoingConnect( bool cleanSession, ProtocolConfiguration pConfig, MQTT3ClientConfiguration config, OutgoingLastWill? lastWill = null,
        uint sessionExpiryInterval = 0, ushort receiveMaximum = ushort.MaxValue, ushort topicAliasMaximum = 0,
        bool requestResponseInfo = false, bool requestProblemInfo = false, IReadOnlyList<UserProperty>? userProperties = null,
        (string authMethod, ReadOnlyMemory<byte> authData)? extendedAuth = null )
    {
        Debug.Assert( pConfig.MaximumPacketSize <= 268435455 );
        _pConfig = pConfig;
        _config = config;
        _lastWill = lastWill;
        _sessionExpiryInterval = sessionExpiryInterval;
        _receiveMaximum = receiveMaximum;
        _topicAliasMaximum = topicAliasMaximum;
        _requestResponseInformation = requestResponseInfo;
        _requestProblemInformation = requestProblemInfo;
        _userProperties = userProperties;
        _extendedAuth = extendedAuth;
        _flags = ByteFlag( config.UserName, config.Password, cleanSession, lastWill );
        _sizePostPayload = config.UserName?.MQTTSize() ?? 0 + config.Password?.MQTTSize() ?? 0;

        if( _pConfig.ProtocolLevel > ProtocolLevel.MQTT3 ) // To compute the size of 
        {
            if( sessionExpiryInterval != 0 ) _propertiesSize += 5;              //https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Ref1477159
            if( receiveMaximum != ushort.MaxValue ) _propertiesSize += 3;       //https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Receive_Maximum
            if( _pConfig.MaximumPacketSize != 268435455 ) _propertiesSize += 5;   //https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc471483569
            if( topicAliasMaximum != 0 ) _propertiesSize += 3;                  //https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc464547825
            if( requestResponseInfo ) _propertiesSize += 2;                     //https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Request_Response_Information
            if( requestProblemInfo ) _propertiesSize += 2;                      //https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc464547827
            if( userProperties != null && userProperties.Count > 0 ) _propertiesSize += (uint)userProperties.Sum( s => s.Size );
            if( extendedAuth.HasValue )
            {
                _propertiesSize += 1 + extendedAuth.Value.authMethod.MQTTSize();
                _propertiesSize += 1 + (uint)extendedAuth.Value.authData.Length + 2;
            }
        }
    }

    protected override uint GetPayloadSize( ProtocolLevel protocolLevel )
        => _sizePostPayload + _lastWill?.GetSize( protocolLevel ) ?? 0;

    protected override byte Header => (byte)PacketType.Connect;

    public override ushort PacketId { get => 0; set => throw new NotSupportedException(); }

    public override QualityOfService Qos => QualityOfService.AtMostOnce;

    public override bool IsRemoteOwnedPacketId => throw new NotSupportedException();

    public override PacketType Type => PacketType.Connect;

    protected override uint GetHeaderSize( ProtocolLevel protocolLevel )
        => _pConfig.ProtocolName.MQTTSize()
            + 1 //_protocolLevel
            + 1 //_flag
            + 2 //_keepAlive
            + (uint)_config.ClientId.MQTTSize()//clientId
            + _propertiesSize
            + (protocolLevel == ProtocolLevel.MQTT3 ? 0 : _propertiesSize.CompactByteCount());

    protected override void WriteHeaderContent( ProtocolLevel protocolLevel, Span<byte> span )
    {
        Debug.Assert( protocolLevel == _pConfig.ProtocolLevel );
        span = span.WriteMQTTString( _pConfig.ProtocolName ); //protocol name: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349225
        // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349228
        span[0] = (byte)_pConfig.ProtocolLevel; //http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349230

        span[1] = _flags;
        span = span[2..];
        BinaryPrimitives.WriteUInt16BigEndian( span, _config.KeepAliveSeconds ); //http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349238
        span = span[2..];
        if( protocolLevel == ProtocolLevel.MQTT5 )
        {
            span = span.WriteVariableByteInteger( _propertiesSize );

            if( _sessionExpiryInterval != 0 )
            {
                span[0] = (byte)PropertyIdentifier.SessionExpiryInterval;
                span = span[1..];
                BinaryPrimitives.WriteUInt32BigEndian( span, _sessionExpiryInterval );
                span = span[4..];
            }
            if( _receiveMaximum != ushort.MaxValue )
            {
                span[0] = (byte)PropertyIdentifier.ReceiveMaximum;
                span = span[1..];
                BinaryPrimitives.WriteUInt16BigEndian( span, _receiveMaximum );
                span = span[2..];
            }
            if( _pConfig.MaximumPacketSize != 268435455 )
            {
                span[0] = (byte)PropertyIdentifier.MaximumPacketSize;
                span = span[1..];
                BinaryPrimitives.WriteUInt32BigEndian( span, _pConfig.MaximumPacketSize );
                span = span[4..];
            }
            if( _topicAliasMaximum != 0 )
            {
                span[0] = (byte)PropertyIdentifier.TopicAliasMaximum;
                span = span[1..];
                BinaryPrimitives.WriteUInt16BigEndian( span, _topicAliasMaximum );
                span = span[2..];
            }
            if( _requestResponseInformation )
            {
                span[0] = (byte)PropertyIdentifier.RequestResponseInformation;
                span[1] = 1;
                span = span[2..];
            }
            if( _requestProblemInformation )
            {
                span[0] = (byte)PropertyIdentifier.RequestProblemInformation;
                span[1] = 1;
                span = span[2..];
            }

            if( _userProperties != null && _userProperties.Count > 0 )
            {
                foreach( var prop in _userProperties )
                {
                    span = prop.Write( span );
                }
            }
            if( _extendedAuth.HasValue )
            {
                span[0] = (byte)PropertyIdentifier.AuthenticationMethod;
                span = span[1..].WriteMQTTString( _extendedAuth.Value.authMethod );
                span[0] = (byte)PropertyIdentifier.AuthenticationData;
                span = span[1..].WriteBinaryData( _extendedAuth.Value.authData );
            }
        }
        span = span.WriteMQTTString( _config.ClientId );
        Debug.Assert( span.Length == 0 );
    }

    protected override async ValueTask WritePayloadAsync( ProtocolLevel protocolLevel, PipeWriter pw, CancellationToken cancellationToken )
    {
        if( _lastWill != null )
        {
            await _lastWill.WriteAsync( protocolLevel, pw, cancellationToken );
        }
        WriteEndOfPayload( pw );
    }

    void WriteEndOfPayload( PipeWriter pw )
    {
        if( _sizePostPayload == 0 ) return;
        Span<byte> span = pw.GetSpan( (int)_sizePostPayload );
        if( (_config.UserName) != null ) span = span.WriteMQTTString( _config.UserName );
        if( (_config.Password) != null ) span.WriteMQTTString( _config.Password );
        pw.Advance( (int)_sizePostPayload );
    }
}
