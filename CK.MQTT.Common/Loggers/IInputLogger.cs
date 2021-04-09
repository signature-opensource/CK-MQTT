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
        IDisposable? ReflexTimeout();
        IDisposable? IncomingPacket( byte header, int length );
        void EndOfStream();
        void UnexpectedEndOfStream();
        IDisposable? ProcessPublishPacket( InputPump sender, byte header, int packetLength, PipeReader reader, Func<ValueTask> next, QualityOfService qos );
        IDisposable? ProcessPacket( PacketType packetType );
        void QueueFullPacketDropped( PacketType packetType, int packetId );
        void InvalidDataReceived( DisconnectedReason reason );
        void UnparsedExtraBytes( InputPump incomingMessageHandler, PacketType packetType, byte header, int packetSize, int unparsedSize );
        void UnparsedExtraBytesPacketId( int unparsedSize );
        void ReadCancelled( int requestedByteCount );
        void UnexpectedEndOfStream( int requestedByteCount, int availableByteCount );
        void PingReqTimeout();
        void DoubleFreePacketId( int packetId );
        void FreedPacketId( int packetId );
        void ConnectionUnknownException( Exception e );
        void ConnectPropertyFieldDuplicated( PropertyIdentifier propertyIdentifier );
        void InvalidMaxPacketSize( int maxPacketSize );
        void InvalidPropertyType();
        void InvalidPropertyValue( PropertyIdentifier propertyIdentifier, object value );
        void ErrorAuthDataMissing();
        void PacketMarkedAsDropped( int currId );
    }
}
