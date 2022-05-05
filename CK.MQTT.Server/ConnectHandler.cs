using CK.MQTT.Client;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public class ConnectHandler : IConnectInfo
    {
        readonly List<(string, string)> _userProperties = new();
        ReadOnlyMemory<byte> _authData;
        string _protocolName = null!; // See TODO below.
        string _clientId = null!;
        string? _currentUserPropKey;
        string? _authentificationMethod;
        string? _userName;
        string? _password;
        uint _fieldCount = 0;
        uint _propertiesLength;
        uint _maxPacketSize;
        uint _sessionExpiryInterval;
        ushort _receiveMaximum = ushort.MaxValue;
        ushort _topicAliasMaximum = 0;
        ushort _keepAlive;
        bool _requestResponseInformation;
        bool _requestProblemInformation;
        bool _authDataRead;
        bool _requestProblemInformationRead;
        bool _requestResponseInformationRead;
        bool _topicAliasMaxiumRead;
        bool _sessionExpiryIntervalRead;
        byte _protocolLevel; //if the connection cannot be made, we default to MQTT3.
        PropertyIdentifier _currentProp;
        byte _flags;

        public async ValueTask<(ConnectReturnCode, ProtocolLevel)> HandleAsync( PipeReader reader, IAuthenticationProtocolHandler securityManager, CancellationToken cancellationToken )
        {
            ReadResult res;
            uint currentStep = 0;
            byte header;
            uint length;
            OperationStatus status;
            while( true )
            {
                res = await reader.ReadAsync( cancellationToken );
                status = InputPump.TryParsePacketHeader( res.Buffer, out header, out length, out SequencePosition position );
                if( status == OperationStatus.Done )
                {
                    reader.AdvanceTo( position );
                    break;
                }
                if( status != OperationStatus.NeedMoreData )
                {
                    return (ConnectReturnCode.Unknown, ProtocolLevel.MQTT3);
                }
                reader.AdvanceTo( res.Buffer.Start, position );
            }
            if( header != 0x10 ) return (ConnectReturnCode.Unknown, 0);

            status = OperationStatus.NeedMoreData;
            while( status != OperationStatus.Done )
            {
                res = await reader.ReadAsync( cancellationToken );

                void ParseAndAdvance() // Trick to use SequenceReader inside an async method.
                {
                    SequenceReader<byte> sequenceReader = new( res.Buffer );
                    // We need the ClientID to instantiate the store to the client.
                    status = ParseFirstPart( ref sequenceReader );
                    if( status == OperationStatus.NeedMoreData )
                    {
                        reader.AdvanceTo( sequenceReader.Position, res.Buffer.End );
                    }
                    else
                    {
                        reader.AdvanceTo( sequenceReader.Position );
                    }
                }
                ParseAndAdvance();
                if( status == OperationStatus.InvalidData ) return (ConnectReturnCode.Unknown, 0); // TODO: log this "Invalid data while parsing the Connect packet.";

                if( currentStep <= 6 && _fieldCount > 6 )
                {
                    if( !await securityManager.ChallengeClientIdAsync( _clientId ) ) return (ConnectReturnCode.IdentifierRejected, ProtocolLevel);
                }

                if( currentStep <= 2 && _fieldCount > 2 )
                {
                    if( !await securityManager.ChallengeShouldHaveCredsAsync( HasUserName, HasPassword ) ) return (ConnectReturnCode.BadUserNameOrPassword, ProtocolLevel);
                }
                if( UserName != null && currentStep <= 10 && _fieldCount > 10 )
                {
                    if( !await securityManager.ChallengeUserNameAsync( UserName ) ) return (ConnectReturnCode.BadUserNameOrPassword, ProtocolLevel);
                }
                if( Password != null && currentStep <= 11 && _fieldCount > 11 )
                {
                    if( !await securityManager.ChallengePasswordAsync( Password ) ) return (ConnectReturnCode.BadUserNameOrPassword, ProtocolLevel);
                }
                currentStep = _fieldCount;
            }
            // TODO:
            // - Last Will
            //      We need to:
            //      Parse last will properties.
            //      Parse last will topic
            //      Store the last will in a store.
            //      Store that doesn't exist.
            // - AUTHENTICATE Packet
            //      Set the next reflex to an Authenticate Handler and handle authentication.

            return (ConnectReturnCode.Accepted, ProtocolLevel);
        }

        public bool HasUserName => (_flags & 0b1000_0000) != 0;
        public bool HasPassword => (_flags & 0b0100_0000) != 0;
        public bool Retain => (_flags & 0b0010_0000) != 0;
        public QualityOfService QoS => (QualityOfService)(_flags << 3 >> 6); // 3 shift on the left to delete the 3 flags on the right. 
        public bool HasLastWill => (_flags & 0b0000_0100) != 0;
        public bool CleanSession => (_flags & 0b0000_0010) != 0;
        public IReadOnlyList<(string, string)> UserProperties => _userProperties;
        public uint MaxPacketSize => _maxPacketSize;
        public uint SessionExpiryInterval => _sessionExpiryInterval;
        public ushort ReceiveMaximum => _receiveMaximum;
        public ushort TopicAliasMaximum => _topicAliasMaximum;
        public bool RequestResponseInformation => _requestResponseInformation;
        public bool RequestProblemInformation => _requestProblemInformation;
        public string? AuthenticationMethod => _authentificationMethod;
        public ReadOnlyMemory<byte> AuthData => _authData;
        public string ClientId => _clientId;

        public ushort KeepAlive => _keepAlive;
        public string ProtocolName => _protocolName;
        public ProtocolLevel ProtocolLevel => (ProtocolLevel)_protocolLevel;
        public string? UserName => _userName;
        public string? Password => _password;

        OperationStatus ParseFirstPart( ref SequenceReader<byte> sequenceReader )
        {
            if( _fieldCount == 0 )
            {
                var pos = sequenceReader.Position;
                if( !sequenceReader.TryReadMQTTString( out _protocolName! ) )
                {
                    sequenceReader.Rewind( sequenceReader.Sequence.Slice( pos, sequenceReader.Position ).Length );
                    return OperationStatus.NeedMoreData;
                }
                _fieldCount++;
            }
            if( _fieldCount == 1 )
            {
                if( !sequenceReader.TryRead( out _protocolLevel ) ) return OperationStatus.NeedMoreData;
                _fieldCount++;
            }
            if( _fieldCount == 2 )
            {
                if( !sequenceReader.TryRead( out _flags ) ) return OperationStatus.NeedMoreData;
                _fieldCount++;
            }
            if( _fieldCount == 3 )
            {
                if( !sequenceReader.TryReadBigEndian( out _keepAlive ) ) return OperationStatus.NeedMoreData;
                _fieldCount = ProtocolLevel == ProtocolLevel.MQTT3 ? 6u : 4;
            }

            if( _fieldCount == 4 )
            {
                if( !sequenceReader.TryReadBigEndian( out _propertiesLength ) ) return OperationStatus.NeedMoreData;
                _fieldCount++;
            }

            if( _fieldCount == 5 )
            {
                OperationStatus res = ParsePropertiesFields( ref sequenceReader );
                if( res == OperationStatus.InvalidData ) return OperationStatus.InvalidData;
                _fieldCount++;
            }

            if( _fieldCount == 6 )
            {
                if( !sequenceReader.TryReadMQTTString( out _clientId! ) ) return OperationStatus.NeedMoreData;
                _fieldCount = ProtocolLevel == ProtocolLevel.MQTT3 ? 8u : 7;
            }
            return OperationStatus.Done;
        }

        OperationStatus ParseLastWill( ref SequenceReader<byte> sequenceReader )
        {
            if( _fieldCount == 7 ) // Last Will Properties.
            {
                if( HasLastWill )
                {
                    throw new NotImplementedException( "TODO" );//TODO.
                }
                _fieldCount++;
            }
            if( _fieldCount == 8 )
            {
                if( HasLastWill ) //Last will topic
                {
                    throw new NotImplementedException( "TODO" );//TODO.
                }
                _fieldCount++;
            }

            if( _fieldCount == 9 )
            {
                if( HasLastWill ) //Last will payload
                {
                    throw new NotImplementedException( "TODO" );//TODO.
                }
                _fieldCount++;
            }

            if( _fieldCount == 10 )
            {
                if( HasUserName && !sequenceReader.TryReadMQTTString( out _userName ) ) return OperationStatus.NeedMoreData;
                _fieldCount++;
            }

            if( _fieldCount == 11 )
            {
                if( HasPassword && !sequenceReader.TryReadMQTTString( out _password ) ) return OperationStatus.NeedMoreData;
            }
            return OperationStatus.Done;
        }


        OperationStatus ParsePropertiesFields( ref SequenceReader<byte> sequenceReader )
        {
            SequencePosition sequencePosition;
            if( sequenceReader.Remaining > _propertiesLength )
            {
                sequencePosition = sequenceReader.Sequence.GetPosition( _propertiesLength, sequenceReader.Position );
            }
            else
            {
                sequencePosition = default;
            }
            while( !sequenceReader.Position.Equals( sequencePosition ) )
            {
                if( _currentProp == PropertyIdentifier.None )
                {
                    if( !sequenceReader.TryRead( out byte val ) ) return OperationStatus.NeedMoreData;

                    _currentProp = (PropertyIdentifier)val;
                }
                switch( _currentProp )
                {
                    case PropertyIdentifier.SessionExpiryInterval:
                        if( _sessionExpiryIntervalRead ) return OperationStatus.InvalidData;
                        if( !sequenceReader.TryReadBigEndian( out _sessionExpiryInterval ) ) return OperationStatus.NeedMoreData;
                        _sessionExpiryIntervalRead = true;
                        _propertiesLength -= 5;
                        break;
                    case PropertyIdentifier.AuthenticationMethod:
                        if( _authentificationMethod != null ) return OperationStatus.InvalidData;

                        if( !sequenceReader.TryReadMQTTString( out _authentificationMethod ) ) return OperationStatus.NeedMoreData;
                        break;
                    case PropertyIdentifier.AuthenticationData:
                        if( _authDataRead ) return OperationStatus.InvalidData;
                        if( !sequenceReader.TryReadMQTTBinaryData( out _authData ) ) return OperationStatus.NeedMoreData;
                        break;
                    case PropertyIdentifier.RequestProblemInformation:
                        if( _requestProblemInformationRead ) return OperationStatus.InvalidData;

                        if( !sequenceReader.TryRead( out byte val1 ) ) return OperationStatus.NeedMoreData;
                        if( val1 > 1 ) return OperationStatus.InvalidData;
                        _requestProblemInformationRead = true;
                        _requestProblemInformation = val1 == 1;
                        break;
                    case PropertyIdentifier.RequestResponseInformation:
                        if( _requestResponseInformationRead ) return OperationStatus.InvalidData;

                        if( !sequenceReader.TryRead( out byte val2 ) ) return OperationStatus.NeedMoreData;
                        if( val2 > 1 ) return OperationStatus.InvalidData;
                        _requestResponseInformationRead = true;
                        _requestResponseInformation = val2 == 1;
                        break;
                    case PropertyIdentifier.ReceiveMaximum:
                        if( ReceiveMaximum != 0 ) return OperationStatus.InvalidData;

                        if( !sequenceReader.TryReadBigEndian( out _receiveMaximum ) ) return OperationStatus.NeedMoreData;
                        if( ReceiveMaximum == 0 ) return OperationStatus.InvalidData;
                        _propertiesLength -= 3;
                        break;
                    case PropertyIdentifier.TopicAliasMaximum:
                        if( _topicAliasMaxiumRead ) return OperationStatus.InvalidData;

                        if( !sequenceReader.TryReadBigEndian( out _topicAliasMaximum ) ) return OperationStatus.NeedMoreData;
                        _topicAliasMaxiumRead = true;
                        break;
                    case PropertyIdentifier.UserProperty:
                        if( _currentUserPropKey == null )
                        {
                            if( !sequenceReader.TryReadMQTTString( out _currentUserPropKey ) ) return OperationStatus.NeedMoreData;
                        }
                        if( !sequenceReader.TryReadMQTTString( out string? propValue ) ) return OperationStatus.NeedMoreData;
                        _userProperties.Add( (_currentUserPropKey, propValue) );
                        _currentUserPropKey = null;
                        break;
                    case PropertyIdentifier.MaximumPacketSize:

                        if( !sequenceReader.TryReadBigEndian( out _maxPacketSize ) ) return OperationStatus.NeedMoreData;
                        if( MaxPacketSize < 1 ) return OperationStatus.InvalidData;
                        break;
                    default:
                        return OperationStatus.InvalidData;
                }
            }
            if( _authDataRead && _authentificationMethod == null ) return OperationStatus.InvalidData;
            return OperationStatus.Done;
        }
    }
}
