using CK.MQTT.Pumps;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IInputLogger
    {
        IDisposable InputLoopStarting();
        void ReadLoopTokenCancelled();
        void InvalidIncomingData();
        void ExceptionOnParsingIncomingData( Exception e );
        void LoopCanceledException( Exception e );
        IDisposable? IncomingPacket( byte header, uint length );
        void EndOfStream();
        void UnexpectedEndOfStream();
        IDisposable? ProcessPublishPacket( InputPump sender, byte header, uint packetLength, PipeReader reader, Func<ValueTask<OperationStatus>> next, QualityOfService qos );
        IDisposable? ProcessPacket( PacketType packetType );
        void QueueFullPacketDropped( PacketType packetType, uint packetId );
        void UnparsedExtraBytes( InputPump incomingMessageHandler, PacketType packetType, byte header, uint packetSize, uint unparsedSize );
        void UnparsedExtraBytesPacketId( uint unparsedSize );
        void ReadCancelled( uint requestedByteCount );
        void UnexpectedEndOfStream( uint requestedByteCount, uint availableByteCount );
        void FreedPacketId( uint packetId );
        void ConnectionUnknownException( Exception e );
        void PacketMarkedAsDropped( uint packetId );
        void UncertainPacketFreed( uint packetId );
        void RentingBytesStore( uint packetSize, IOutgoingPacket packet );
        IDisposable? SerializingPacketInMemory( IOutgoingPacket packet );
        void ConnectPropertyFieldDuplicated( PropertyIdentifier sessionExpiryInterval );
        void InvalidPropertyValue( PropertyIdentifier requestResponseInformation, byte val1 );
        void InvalidMaxPacketSize( uint maxPacketSize );
        void InvalidPropertyType();
        void ErrorAuthDataMissing();
        void ProtocolViolation( ProtocolViolationException e );
        void ReflexSignaledInvalidData();
    }
}
