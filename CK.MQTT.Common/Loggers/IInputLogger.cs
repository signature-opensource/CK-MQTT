using CK.MQTT.Pumps;
using System;
using System.IO.Pipelines;
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
        IDisposable? IncomingPacket( byte header, int length );
        void EndOfStream();
        void UnexpectedEndOfStream();
        IDisposable? ProcessPublishPacket( InputPump sender, byte header, int packetLength, PipeReader reader, Func<ValueTask> next, QualityOfService qos );
        IDisposable? ProcessPacket( PacketType packetType );
        void QueueFullPacketDropped( PacketType packetType, int packetId );
        void UnparsedExtraBytes( InputPump incomingMessageHandler, PacketType packetType, byte header, int packetSize, int unparsedSize );
        void UnparsedExtraBytesPacketId( int unparsedSize );
        void ReadCancelled( int requestedByteCount );
        void UnexpectedEndOfStream( int requestedByteCount, int availableByteCount );
        void FreedPacketId( int packetId );
        void ConnectionUnknownException( Exception e );
        void PacketMarkedAsDropped( int packetId );
        void UncertainPacketFreed( int packetId );
        void RentingBytesStore( int packetSize, IOutgoingPacket packet );
        IDisposable? SerializingPacketInMemory( IOutgoingPacket packet );
        void ConnectPropertyFieldDuplicated( PropertyIdentifier sessionExpiryInterval );
        void InvalidPropertyValue( PropertyIdentifier requestResponseInformation, byte val1 );
        void InvalidMaxPacketSize( int maxPacketSize );
        void InvalidPropertyType();
        void ErrorAuthDataMissing();
    }
}
