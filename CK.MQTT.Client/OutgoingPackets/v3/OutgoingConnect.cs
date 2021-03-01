using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CK.MQTT.IOutgoingPacket;

namespace CK.MQTT
{
    class OutgoingConnect : ComplexOutgoingPacket
    {
        readonly MqttClientConfiguration _mConf;
        readonly MqttClientCredentials? _creds;
        readonly OutgoingLastWill? _lastWill;
        readonly uint _sessionExpiryInterval;
        readonly ushort _receiveMaximum;
        readonly uint _maximumPacketSize;
        readonly ushort _topicAliasMaximum;
        readonly bool _requestResponseInformation;
        readonly bool _requestProblemInformation;
        readonly IReadOnlyList<UserProperty>? _userProperties;
        readonly (string authMethod, ReadOnlyMemory<byte> authData)? _extendedAuth;
        readonly ProtocolConfiguration _pConf;
        readonly byte _flags;
        readonly int _sizePostPayload;
        readonly int _propertiesSize;
        static byte ByteFlag( MqttClientCredentials? creds, OutgoingLastWill? lastWill )
        {
            byte flags = 0;
            if( creds?.UserName != null ) flags |= 0b1000_0000; // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Figure_3.4_-
            if( creds?.Password != null ) flags |= 0b0100_0000;
            if( lastWill?.Retain ?? false ) flags |= 0b0010_0000;
            flags |= (byte)((byte)(lastWill?.Qos ?? 0) << 3);
            if( lastWill != null ) flags |= 0b0000_0100;
            if( creds?.CleanSession ?? true ) flags |= 0b0000_0010;
            return flags;
        }

        public OutgoingConnect(
            ProtocolConfiguration pConf, MqttClientConfiguration mConf, MqttClientCredentials? creds, OutgoingLastWill? lastWill = null,
            uint sessionExpiryInterval = 0, ushort receiveMaximum = ushort.MaxValue, uint maximumPacketSize = 268435455, ushort topicAliasMaximum = 0,
            bool requestResponseInfo = false, bool requestProblemInfo = false, IReadOnlyList<UserProperty>? userProperties = null,
            (string authMethod, ReadOnlyMemory<byte> authData)? extendedAuth = null )
        {
            Debug.Assert( maximumPacketSize <= 268435455 );
            (_pConf, _mConf, _creds, _lastWill, _sessionExpiryInterval, _receiveMaximum, _maximumPacketSize,
                _topicAliasMaximum, _requestResponseInformation, _requestProblemInformation, _userProperties, _extendedAuth)
            = (pConf, mConf, creds, lastWill, sessionExpiryInterval, receiveMaximum, maximumPacketSize,
                topicAliasMaximum, requestResponseInfo, requestProblemInfo, userProperties, extendedAuth);
            _flags = ByteFlag( creds, lastWill );
            _sizePostPayload = creds?.UserName?.MQTTSize() ?? 0 + creds?.Password?.MQTTSize() ?? 0;

            if( pConf.ProtocolLevel > ProtocolLevel.MQTT3 ) // To compute the size of 
            {
                if( sessionExpiryInterval != 0 ) _propertiesSize += 5;          //https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Ref1477159
                if( receiveMaximum != ushort.MaxValue ) _propertiesSize += 3;   //https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Receive_Maximum
                if( maximumPacketSize != 268435455 ) _propertiesSize += 5;      //https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc471483569
                if( topicAliasMaximum != 0 ) _propertiesSize += 3;              //https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc464547825
                if( requestResponseInfo ) _propertiesSize += 2;                 //https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Request_Response_Information
                if( requestProblemInfo ) _propertiesSize += 2;                  //https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc464547827
                if( userProperties != null && userProperties.Count > 0 ) _propertiesSize += userProperties.Sum( s => s.Size );
                if( extendedAuth.HasValue )
                {
                    _propertiesSize += 1 + extendedAuth.Value.authMethod.MQTTSize();
                    _propertiesSize += 1 + extendedAuth.Value.authData.Length + 2;
                }
            }
        }

        protected override int GetPayloadSize( ProtocolLevel protocolLevel )
            => _sizePostPayload + _lastWill?.GetSize( protocolLevel ) ?? 0;

        protected override byte Header => (byte)PacketType.Connect;

        protected override int GetHeaderSize( ProtocolLevel protocolLevel )
            => _pConf.ProtocolName.MQTTSize()
                + 1 //_protocolLevel
                + 1 //_flag
                + 2 //_keepAlive
                + (_creds?.ClientId.MQTTSize() ?? 2)//clientId
                + _propertiesSize
                + (protocolLevel == ProtocolLevel.MQTT3 ? 0 : _propertiesSize.CompactByteCount());

        protected override void WriteHeaderContent( ProtocolLevel protocolLevel, Span<byte> span )
        {
            Debug.Assert( protocolLevel == _pConf.ProtocolLevel );
            span = span.WriteMQTTString( _pConf.ProtocolName ); //protocol name: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349225
            // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349228
            span[0] = (byte)_pConf.ProtocolLevel; //http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349230

            span[1] = _flags;
            span = span[2..].WriteBigEndianUInt16( _mConf.KeepAliveSeconds );//http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349238
            if( protocolLevel == ProtocolLevel.MQTT5 )
            {
                span = span.WriteVariableByteInteger( _propertiesSize );

                if( _sessionExpiryInterval != 0 )
                {
                    span[0] = (byte)PropertyIdentifier.SessionExpiryInterval;
                    span = span[1..].WriteBigEndianUInt32( _sessionExpiryInterval );
                }
                if( _receiveMaximum != ushort.MaxValue )
                {
                    span[0] = (byte)PropertyIdentifier.ReceiveMaximum;
                    span = span[1..].WriteBigEndianUInt16( _receiveMaximum );
                }
                if( _maximumPacketSize != 268435455 )
                {
                    span[0] = (byte)PropertyIdentifier.MaximumPacketSize;
                    span = span[1..].WriteBigEndianUInt32( _maximumPacketSize );
                }
                if( _topicAliasMaximum != 0 )
                {
                    span[0] = (byte)PropertyIdentifier.TopicAliasMaximum;
                    span = span[1..].WriteBigEndianUInt16( _topicAliasMaximum );
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
            span = span.WriteMQTTString( _creds?.ClientId ?? "" );
            Debug.Assert( span.Length == 0 );
        }

        protected override async ValueTask<WriteResult> WritePayloadAsync( ProtocolLevel protocolLevel, PipeWriter pw, CancellationToken cancellationToken )
        {
            if( _lastWill != null )
            {
                WriteResult res = await _lastWill.WriteAsync( protocolLevel, pw, cancellationToken );
                if( res != WriteResult.Written ) throw new InvalidOperationException( "Last will was not written." );
            }
            WriteEndOfPayload( pw );
            await pw.FlushAsync( cancellationToken );
            return WriteResult.Written;
        }

        void WriteEndOfPayload( PipeWriter pw )
        {
            if( _sizePostPayload == 0 ) return;
            Span<byte> span = pw.GetSpan( _sizePostPayload );
            if( (_creds?.UserName) != null ) span = span.WriteMQTTString( _creds.UserName );
            if( (_creds?.Password) != null ) span.WriteMQTTString( _creds.Password );
            pw.Advance( _sizePostPayload );
        }
    }
}
