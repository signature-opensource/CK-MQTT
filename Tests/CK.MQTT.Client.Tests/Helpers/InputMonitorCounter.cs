using CK.MQTT.Pumps;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    [ExcludeFromCodeCoverage]
    class InputMonitorCounter : IInputLogger
    {
        readonly IInputLogger _inputLogger;

        public InputMonitorCounter( IInputLogger inputLogger )
        {
            _inputLogger = inputLogger;
        }
        public int ConnectionUnknownExceptionCounter { get; private set; } = 0;
        public void ConnectionUnknownException( Exception e )
        {
            ConnectionUnknownExceptionCounter++;
            _inputLogger.ConnectionUnknownException( e );
        }
        public int ConnectPropertyFieldDuplicatedCounter { get; private set; } = 0;
        public void ConnectPropertyFieldDuplicated( PropertyIdentifier sessionExpiryInterval )
        {
            ConnectPropertyFieldDuplicatedCounter++;
            _inputLogger.ConnectPropertyFieldDuplicated( sessionExpiryInterval );
        }
        public int EndOfStreamCounter { get; private set; } = 0;
        public void EndOfStream()
        {
            EndOfStreamCounter++;
            _inputLogger.EndOfStream();
        }
        public int ErrorAuthDataMissingCounter { get; private set; } = 0;
        public void ErrorAuthDataMissing()
        {
            ErrorAuthDataMissingCounter++;
            _inputLogger.ErrorAuthDataMissing();
        }
        public int ExceptionOnParsingIncomingDataCounter { get; private set; } = 0;
        public void ExceptionOnParsingIncomingData( Exception e )
        {
            ExceptionOnParsingIncomingDataCounter++;
            _inputLogger.ExceptionOnParsingIncomingData( e );
        }
        public int FreedPacketIdCounter { get; private set; } = 0;
        public void FreedPacketId( int packetId )
        {
            FreedPacketIdCounter++;
            _inputLogger.FreedPacketId( packetId );
        }
        public int IncomingPacketCounter { get; private set; } = 0;
        public IDisposable? IncomingPacket( byte header, int length )
        {
            IncomingPacketCounter++;
            return _inputLogger.IncomingPacket( header, length );
        }
        public int InputLoopStartingCounter { get; private set; } = 0;
        public IDisposable InputLoopStarting()
        {
            InputLoopStartingCounter++;
            return _inputLogger.InputLoopStarting();
        }
        public int InvalidIncomingDataCounter { get; private set; } = 0;
        public void InvalidIncomingData()
        {
            InvalidIncomingDataCounter++;
            _inputLogger.InvalidIncomingData();
        }
        public int InvalidMaxPacketSizeCounter { get; private set; } = 0;
        public void InvalidMaxPacketSize( int maxPacketSize )
        {
            InvalidMaxPacketSizeCounter++;
            _inputLogger.InvalidMaxPacketSize( maxPacketSize );
        }
        public int InvalidPropertyTypeCounter { get; private set; } = 0;
        public void InvalidPropertyType()
        {
            InvalidPropertyTypeCounter++;
            _inputLogger.InvalidPropertyType();
        }
        public int InvalidPropertyValueCounter { get; private set; } = 0;
        public void InvalidPropertyValue( PropertyIdentifier requestResponseInformation, byte val1 )
        {
            InvalidPropertyValueCounter++;
            _inputLogger.InvalidPropertyValue( requestResponseInformation, val1 );
        }
        public int LoopCanceledExceptionCounter { get; private set; } = 0;
        public void LoopCanceledException( Exception e )
        {
            LoopCanceledExceptionCounter++;
            _inputLogger.LoopCanceledException( e );
        }
        public int PacketMarkedAsDroppedCounter { get; private set; } = 0;
        public void PacketMarkedAsDropped( int packetId )
        {
            PacketMarkedAsDroppedCounter++;
            _inputLogger.PacketMarkedAsDropped( packetId );
        }
        public int ProcessPacketCounter { get; private set; } = 0;
        public IDisposable? ProcessPacket( PacketType packetType )
        {
            ProcessPacketCounter++;
            return _inputLogger.ProcessPacket( packetType );
        }
        public int ProcessPublishPacketCounter { get; private set; } = 0;
        public IDisposable? ProcessPublishPacket( InputPump sender, byte header, int packetLength, PipeReader reader, Func<ValueTask> next, QualityOfService qos )
        {
            ProcessPublishPacketCounter++;
            return _inputLogger.ProcessPublishPacket( sender, header, packetLength, reader, next, qos );
        }
        public int QueueFullPacketDroppedCounter { get; private set; } = 0;
        public void QueueFullPacketDropped( PacketType packetType, int packetId )
        {
            QueueFullPacketDroppedCounter++;
            _inputLogger.QueueFullPacketDropped( packetType, packetId );
        }
        public int ReadCancelledCounter { get; private set; } = 0;
        public void ReadCancelled( int requestedByteCount )
        {
            ReadCancelledCounter++;
            _inputLogger.ReadCancelled( requestedByteCount );
        }
        public int ReadLoopTokenCancelledCounter { get; private set; } = 0;
        public void ReadLoopTokenCancelled()
        {
            ReadLoopTokenCancelledCounter++;
            _inputLogger.ReadLoopTokenCancelled();
        }
        public int RentingBytesStoreCounter { get; private set; } = 0;
        public void RentingBytesStore( int packetSize, IOutgoingPacket packet )
        {
            RentingBytesStoreCounter++;
            _inputLogger.RentingBytesStore( packetSize, packet );
        }
        public int SerializingPacketInMemoryCounter { get; private set; } = 0;
        public IDisposable? SerializingPacketInMemory( IOutgoingPacket packet )
        {
            SerializingPacketInMemoryCounter++;
            return _inputLogger.SerializingPacketInMemory( packet );
        }
        public int UncertainPacketFreedCounter { get; private set; } = 0;
        public void UncertainPacketFreed( int packetId )
        {
            UncertainPacketFreedCounter++;
            _inputLogger.UncertainPacketFreed( packetId );
        }
        /// <summary>
        /// UnexpectedEndOfStream()
        /// </summary>
        public int UnexpectedEndOfStreamCounter1 { get; private set; } = 0;
        public void UnexpectedEndOfStream()
        {
            UnexpectedEndOfStreamCounter1++;
            _inputLogger.UnexpectedEndOfStream();
        }
        /// <summary>
        /// UnexpectedEndOfStream( int requestedByteCount, int availableByteCount )
        /// </summary>
        public int UnexpectedEndOfStreamCounter2 { get; private set; } = 0;
        public void UnexpectedEndOfStream( int requestedByteCount, int availableByteCount )
        {
            UnexpectedEndOfStreamCounter2++;
            _inputLogger.UnexpectedEndOfStream( requestedByteCount, availableByteCount );
        }
        public int UnparsedExtraBytesCounter { get; private set; } = 0;
        public void UnparsedExtraBytes( InputPump incomingMessageHandler, PacketType packetType, byte header, int packetSize, int unparsedSize )
        {
            UnparsedExtraBytesCounter++;
            _inputLogger.UnparsedExtraBytes( incomingMessageHandler, packetType, header, packetSize, unparsedSize );
        }
        public int UnparsedExtraBytesPacketIdCounter { get; private set; } = 0;
        public void UnparsedExtraBytesPacketId( int unparsedSize )
        {
            UnparsedExtraBytesPacketIdCounter++;
            _inputLogger.UnparsedExtraBytesPacketId( unparsedSize );
        }

        public int ProtocolViolationCounter { get; private set; } = 0;
        public void ProtocolViolation( ProtocolViolationException e )
        {
            ProtocolViolationCounter++;
            _inputLogger.ProtocolViolation( e );
        }
    }
}
