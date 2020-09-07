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
        readonly MqttConfiguration _mConf;
        readonly MqttClientCredentials? _creds;
        private readonly OutgoingLastWill? _lastWill;
        readonly uint _sessionExpiryInterval;
        readonly ushort _receiveMaximum;
        readonly uint _maximumPacketSize;
        readonly ushort _topicAliasMaximum;
        readonly bool _requestResponseInformation;
        readonly bool _requestProblemInformation;
        readonly IReadOnlyList<UserProperty>? _userProperties;
        readonly (string authentificationMethod, ReadOnlyMemory<byte> authentificationData)? _extendedAuth;
        readonly ProtocolConfiguration _pConf;
        readonly byte _flags;
        readonly int _sizePostPayload;
        readonly int _propertiesSize;
        const byte _usernameFlag = 0b1000_0000;
        const byte _passwordFlag = 0b0100_0000;
        const byte _willRetainFlag = 0b0010_0000;
        const byte _willFlag = 0b0000_0100;
        const byte _cleanSessionFlag = 0b0000_0010;
        static byte ByteFlag( MqttClientCredentials? creds, OutgoingLastWill? lastWill )
        {
            byte flags = 0;
            if( creds?.UserName != null ) flags |= _usernameFlag;
            if( creds?.Password != null ) flags |= _passwordFlag;
            if( lastWill?.Retain ?? false ) flags |= _willRetainFlag;
            flags |= (byte)((byte)(lastWill?.Qos ?? 0) << 3);
            if( lastWill != null ) flags |= _willFlag;
            if( creds?.CleanSession ?? true ) flags |= _cleanSessionFlag;
            return flags;
        }

        public OutgoingConnect(
            ProtocolConfiguration pConf,
            MqttConfiguration mConf,
            MqttClientCredentials? creds,
            OutgoingLastWill? lastWill = null,
            uint sessionExpiryInterval = 0,
            ushort receiveMaximum = ushort.MaxValue,
            uint maximumPacketSize = 268435455,
            ushort topicAliasMaximum = 0,
            bool requestResponseInformation = false,
            bool requestProblemInformation = false,
            IReadOnlyList<UserProperty>? userProperties = null,
            (string authentificationMethod, ReadOnlyMemory<byte> authentificationData)? extendedAuth = null )
        {
            Debug.Assert( maximumPacketSize <= 268435455 );
            _pConf = pConf;
            _mConf = mConf;
            _creds = creds;
            _lastWill = lastWill;
            _sessionExpiryInterval = sessionExpiryInterval;
            _receiveMaximum = receiveMaximum;
            _maximumPacketSize = maximumPacketSize;
            _topicAliasMaximum = topicAliasMaximum;
            _requestResponseInformation = requestResponseInformation;
            _requestProblemInformation = requestProblemInformation;
            _userProperties = userProperties;
            _extendedAuth = extendedAuth;
            _flags = ByteFlag( creds, lastWill );
            _sizePostPayload = creds?.UserName?.MQTTSize() ?? 0 + creds?.Password?.MQTTSize() ?? 0;

            if( pConf.ProtocolLevel > ProtocolLevel.MQTT3 )
            {
                if( sessionExpiryInterval != 0 ) _propertiesSize += 5; //https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Ref1477159
                if( receiveMaximum != ushort.MaxValue ) _propertiesSize += 3; //https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Receive_Maximum
                if( maximumPacketSize != 268435455 ) _propertiesSize += 5; //https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc471483569
                if( topicAliasMaximum != 0 ) _propertiesSize += 3; //https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc464547825
                if( requestResponseInformation ) _propertiesSize += 2;//https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Request_Response_Information
                if( requestProblemInformation ) _propertiesSize += 2;//https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc464547827
                if( userProperties != null && userProperties.Count > 0 )
                {
                    _propertiesSize += userProperties.Sum( s => s.Size );
                }
                if( extendedAuth.HasValue )
                {
                    _propertiesSize += 1 + extendedAuth.Value.authentificationMethod.MQTTSize();
                    _propertiesSize += 1 + extendedAuth.Value.authentificationData.Length + 2;
                }
            }
        }

        protected override int GetPayloadSize( ProtocolLevel protocolLevel )
            => _sizePostPayload + _lastWill?.GetSize( protocolLevel ) ?? 0;

        protected override byte Header => (byte)PacketType.Connect;

        protected override int GetHeaderSize( ProtocolLevel protocolLevel )
        {
            return _pConf.ProtocolName.MQTTSize()
                + 1 //_protocolLevel
                + 1 //_flag
                + 2 //_keepAlive
                + (_creds?.ClientId.MQTTSize() ?? 2)//clientId
                + _propertiesSize
                + (protocolLevel == ProtocolLevel.MQTT3 ? 0 : _propertiesSize.CompactByteCount());
        }

        protected override void WriteHeaderContent( ProtocolLevel protocolLevel, Span<byte> span )
        {
            Debug.Assert( protocolLevel == _pConf.ProtocolLevel );

            span = span.WriteMQTTString( _pConf.ProtocolName ); //protocol name: http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349225
            // http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349228
            span[0] = (byte)_pConf.ProtocolLevel; //http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349230

            span[1] = _flags;
            ushort keepAlive = _mConf.KeepAlive == Timeout.InfiniteTimeSpan ? (ushort)0 : (ushort)_mConf.KeepAlive.TotalSeconds;
            span = span[2..].WriteBigEndianUInt16( keepAlive );//http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc385349238
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
                    span = span[1..].WriteMQTTString( _extendedAuth.Value.authentificationMethod );
                    span[0] = (byte)PropertyIdentifier.AuthenticationData;
                    span = span[1..].WriteBinaryData( _extendedAuth.Value.authentificationData );
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
                if( res != WriteResult.Written ) return res;
            }
            WriteEndOfPayload( pw );
            await pw.FlushAsync( cancellationToken );
            return WriteResult.Written;
        }

        void WriteEndOfPayload( PipeWriter pw )
        {
            if( _sizePostPayload == 0 ) return;
            Span<byte> span = pw.GetSpan( _sizePostPayload );
            string? username = _creds?.UserName;
            string? password = _creds?.Password;
            if( username != null ) span = span.WriteMQTTString( username );
            if( password != null ) span.WriteMQTTString( password );
            pw.Advance( _sizePostPayload );
        }
    }
}
