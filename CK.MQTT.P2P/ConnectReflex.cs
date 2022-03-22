using CK.MQTT.Client;
using CK.MQTT.P2P;
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

namespace CK.MQTT.Packets
{
    class ConnectReflex
    {
        readonly List<(string, string)> _userProperties = new();
        readonly TaskCompletionSource _taskCompletionSource = new();
        readonly IMqtt5ServerClientSink _sink;
        readonly ProtocolConfiguration _pConfig;
        readonly P2PMqttConfiguration _config;
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
        byte _protocolLevel;
        PropertyIdentifier _currentProp;
        byte _flags;
        // Currently the parsed data is available when the packet is not parsed yet and can lead to errors.
        readonly SemaphoreSlim _exitWait = new( 0 );
        public ConnectReflex( IMqtt5ServerClientSink sink, ProtocolConfiguration pConfig, P2PMqttConfiguration config )
        {
            _sink = sink;
            _pConfig = pConfig;
            _config = config;
        }

        [MemberNotNull( nameof( InStore ), nameof( OutStore ), nameof( _sender ) )]
#pragma warning disable CS8774 
        public Task ConnectHandledTask => _taskCompletionSource.Task;
#pragma warning restore CS8774 
        InputPump? _sender;

        public async ValueTask<OperationStatus> HandleRequestAsync( IMqtt3Sink sink, InputPump sender, byte header, uint packetSize, PipeReader reader, CancellationToken cancellationToken )
        {
            _sender = sender;
            OperationStatus status = OperationStatus.NeedMoreData;
            ReadResult res;
            while( status != OperationStatus.Done )
            {
                res = await reader.ReadAsync( cancellationToken );
                ParseFirstPartInternal();
                void ParseFirstPartInternal() // Trick to use SequenceReader inside an async method.
                {
                    SequenceReader<byte> sequenceReader = new( res.Buffer );
                    // We need the ClientID to instantiate the store to the client.
                    status = ParseFirstPart( ref sequenceReader );
                    if( status == OperationStatus.InvalidData ) throw new ProtocolViolationException( "Invalid data while parsing the Connect packet." );
                    if( status == OperationStatus.NeedMoreData )
                    {
                        reader.AdvanceTo( sequenceReader.Position, res.Buffer.End );
                    }
                    else
                    {
                        reader.AdvanceTo( sequenceReader.Position );
                    }
                }
            }
            (OutStore, InStore) = await _config.StoreFactory.CreateAsync(  _pConfig, _config, ClientId, CleanSession );
            // TODO:
            // - Last Will
            //      We need to:
            //      Parse last will properties.
            //      Parse last will topic
            //      Store the last will in a store.
            //      Store that doesn't exist.
            // - AUTHENTICATE Packet
            //      Set the next reflex to an Authenticate Handler and handle authentication.
            _taskCompletionSource.SetResult();
            await _exitWait.WaitAsync( cancellationToken );

            return OperationStatus.Done;
        }

        public void EngageNextReflex( Reflex reflex )
        {
            _sender!.CurrentReflex = reflex;
            _exitWait.Release();
        }


        public ILocalPacketStore? OutStore { get; set; }
        public IRemotePacketStore? InStore { get; set; }

        public bool HasUserName => (_flags & 0b1000_0000) != 0;
        public bool HasPassword => (_flags & 0b0100_0000) != 0;
        public bool Retain => (_flags & 0b0010_0000) != 0;
        public QualityOfService QoS => (QualityOfService)((_flags << 3) >> 6); // 3 shift on the left to delete the 3 flags on the right. 
        public bool HasLastWill => (_flags & 0b0000_0100) != 0;
        public bool CleanSession => (_flags & 0b0000_0010) != 0;
        public List<(string, string)> UserProperties => _userProperties;
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
                if( !sequenceReader.TryReadMQTTString( out _protocolName! ) ) return OperationStatus.NeedMoreData;
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
                        UserProperties.Add( (_currentUserPropKey, propValue) );
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
