using CK.Core;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    class ConnectReflex
    {
        readonly ClientInstance _clientInstance;
        TaskCompletionSource<object?> _taskCompletionSource = new TaskCompletionSource<object?>();
        public string ProtocolName;
        public ProtocolLevel ProtocolLevel;
        byte _flags;
        public ushort KeepAlive;
        public string ClientId;
        public ConnectReflex( ClientInstance clientInstance )
        {
            _clientInstance = clientInstance;
        }
        public ValueTask HandleRequest( IInputLogger? m,
            InputPump sender, byte header, int packetSize, PipeReader reader, CancellationToken cancellationToken )
        {

        }

        bool HasUserName => (_flags & 0b1000_0000) != 0;
        bool HasPassword => (_flags & 0b0100_0000) != 0;
        bool Retain => (_flags & 0b0010_0000) != 0;
        QualityOfService QoS => (QualityOfService)((_flags << 3) >> 6); // 3 shift on the left to delete the 3 flags on the right. 
        bool HasLastWill => (_flags & 0b0000_0100) != 0;
        bool CleanSession => (_flags & 0b0000_0010) != 0;
        int _fieldCount = 0;


        OperationStatus Parse( SequenceReader<byte> sequenceReader )
        {
            if( _fieldCount == 0 )
            {
                if( !sequenceReader.TryReadMQTTString( out ProtocolName! ) ) return OperationStatus.NeedMoreData;
                _fieldCount++;
            }
            if( _fieldCount == 1 )
            {
                if( !sequenceReader.TryRead( out byte lvl ) ) return OperationStatus.NeedMoreData;
                ProtocolLevel = (ProtocolLevel)lvl;
                _fieldCount++;
            }

            if( _fieldCount == 2 )
            {
                if( !sequenceReader.TryRead( out _flags ) ) return OperationStatus.NeedMoreData;
                _fieldCount++;
            }

            if( _fieldCount == 3 )
            {
                if( !sequenceReader.TryReadBigEndian( out KeepAlive ) ) return OperationStatus.NeedMoreData;
                _fieldCount++;
            }

            if( ProtocolLevel == ProtocolLevel.MQTT5 )
            {
                if( _fieldCount == 4 )
                {
                    if( !sequenceReader.TryReadMQTTString( out ClientId! ) ) return OperationStatus.NeedMoreData;
                    _fieldCount++;
                }
            }
            else
            {
                if( _fieldCount == 4 )
                {
                    if( !sequenceReader.TryReadBigEndian( out _propertiesLength ) ) return OperationStatus.NeedMoreData;
                    _fieldCount++;
                }

                if( _fieldCount == 5 )
                {
                    // TODO: Loop over fields, decrease the total length and set values.
                }
            }
        }

        PropertyIdentifier _currentProp;

        int _propertiesLength;
        bool _sessionExpiryIntervalRead; // TODO: use int minus as flag.
        public int SessionExpiryInterval = 0;
        bool _receiveMaximumRead;
        public ushort ReceiveMaximum = ushort.MaxValue;
        bool _maxPacketSizeReceive;
        public int MaxPacketSize;
        OperationStatus ParseFields( IActivityMonitor? m, SequenceReader<byte> sequenceReader )
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
                    case PropertyIdentifier.None:
                        m?.Error( "Invalid property type." );
                        return OperationStatus.InvalidData;
                    case PropertyIdentifier.PayloadFormatIndicator:
                        break;
                    case PropertyIdentifier.MessageExpiryInterval:
                        break;
                    case PropertyIdentifier.ContentType:
                        break;
                    case PropertyIdentifier.ResponseTopic:
                        break;
                    case PropertyIdentifier.CorrelationData:
                        break;
                    case PropertyIdentifier.SubscriptionIdentifier:
                        break;
                    case PropertyIdentifier.SessionExpiryInterval:
                        if( _sessionExpiryIntervalRead ) return OperationStatus.InvalidData;
                        if( !sequenceReader.TryReadBigEndian( out SessionExpiryInterval ) ) return OperationStatus.NeedMoreData;
                        _sessionExpiryIntervalRead = true;
                        _propertiesLength -= 5;
                        break;
                    case PropertyIdentifier.AssignedClientIdentifier:
                        break;
                    case PropertyIdentifier.ServerKeepAlive:
                        break;
                    case PropertyIdentifier.AuthenticationMethod:
                        break;
                    case PropertyIdentifier.AuthenticationData:
                        break;
                    case PropertyIdentifier.RequestProblemInformation:
                        break;
                    case PropertyIdentifier.WillDelayInterval:
                        break;
                    case PropertyIdentifier.RequestResponseInformation:
                        break;
                    case PropertyIdentifier.ResponseInformation:
                        break;
                    case PropertyIdentifier.ServerReference:
                        break;
                    case PropertyIdentifier.ReasonString:
                        break;
                    case PropertyIdentifier.ReceiveMaximum:
                        if( _receiveMaximumRead ) return OperationStatus.InvalidData;
                        if( !sequenceReader.TryReadBigEndian( out ReceiveMaximum ) ) return OperationStatus.NeedMoreData;
                        if( ReceiveMaximum == 0 ) return OperationStatus.InvalidData;
                        _receiveMaximumRead = true;
                        _propertiesLength -= 3;
                        break;
                    case PropertyIdentifier.TopicAliasMaximum:
                        break;
                    case PropertyIdentifier.TopicAlias:
                        break;
                    case PropertyIdentifier.MaximumQoS:
                        break;
                    case PropertyIdentifier.RetainAvailable:
                        break;
                    case PropertyIdentifier.UserProperty:
                        break;
                    case PropertyIdentifier.MaximumPacketSize:
                        if( _maxPacketSizeReceive ) return OperationStatus.InvalidData;
                        if( !sequenceReader.TryReadBigEndian( out MaxPacketSize ) ) return OperationStatus.NeedMoreData;

                        break;
                    case PropertyIdentifier.WildcardSubscriptionAvailable:
                        break;
                    case PropertyIdentifier.SubscriptionIdentifierAvailable:
                        break;
                    case PropertyIdentifier.SharedSubscriptionAvailable:
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
