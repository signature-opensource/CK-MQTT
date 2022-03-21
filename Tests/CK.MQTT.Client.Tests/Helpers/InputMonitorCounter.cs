//using CK.Core;
//using CK.MQTT.Pumps;
//using System;
//using System.Buffers;
//using System.Diagnostics.CodeAnalysis;
//using System.IO.Pipelines;
//using System.Net;
//using System.Threading.Tasks;

//namespace CK.MQTT.Client.Tests.Helpers
//{
//    [ExcludeFromCodeCoverage]
//    class InputMonitorCounter : IInputLogger
//    {
//        readonly IInputLogger _inputLogger;

//        public InputMonitorCounter( IInputLogger inputLogger )
//        {
//            _inputLogger = inputLogger;
//        }
//        public uint ConnectionUnknownExceptionCounter { get; private set; } = 0;
//        public void ConnectionUnknownException( Exception e )
//        {
//            ConnectionUnknownExceptionCounter++;
//            _inputLogger.ConnectionUnknownException( e );
//        }
//        public uint ConnectPropertyFieldDuplicatedCounter { get; private set; } = 0;
//        public void ConnectPropertyFieldDuplicated( PropertyIdentifier sessionExpiryInterval )
//        {
//            ConnectPropertyFieldDuplicatedCounter++;
//            _inputLogger.ConnectPropertyFieldDuplicated( sessionExpiryInterval );
//        }
//        public uint EndOfStreamCounter { get; private set; } = 0;
//        public void EndOfStream()
//        {
//            EndOfStreamCounter++;
//            _inputLogger.EndOfStream();
//        }
//        public uint ErrorAuthDataMissingCounter { get; private set; } = 0;
//        public void ErrorAuthDataMissing()
//        {
//            ErrorAuthDataMissingCounter++;
//            _inputLogger.ErrorAuthDataMissing();
//        }
//        public uint ExceptionOnParsingIncomingDataCounter { get; private set; } = 0;
//        public void ExceptionOnParsingIncomingData( Exception e )
//        {
//            ExceptionOnParsingIncomingDataCounter++;
//            _inputLogger.ExceptionOnParsingIncomingData( e );
//        }
//        public uint FreedPacketIdCounter { get; private set; } = 0;
//        public void FreedPacketId( ushort PacketId )
//        {
//            FreedPacketIdCounter++;
//            _inputLogger.FreedPacketId( packetId );
//        }
//        public uint IncomingPacketCounter { get; private set; } = 0;
//        public IDisposable? IncomingPacket( byte header, uint length )
//        {
//            IncomingPacketCounter++;
//            return _inputLogger.IncomingPacket( header, length );
//        }
//        public uint InputLoopStartingCounter { get; private set; } = 0;
//        public IDisposable InputLoopStarting()
//        {
//            InputLoopStartingCounter++;
//            return _inputLogger.InputLoopStarting();
//        }
//        public uint InvalidIncomingDataCounter { get; private set; } = 0;
//        public void InvalidIncomingData()
//        {
//            InvalidIncomingDataCounter++;
//            _inputLogger.InvalidIncomingData();
//        }
//        public uint InvalidMaxPacketSizeCounter { get; private set; } = 0;
//        public void InvalidMaxPacketSize( uint maxPacketSize )
//        {
//            InvalidMaxPacketSizeCounter++;
//            _inputLogger.InvalidMaxPacketSize( maxPacketSize );
//        }
//        public uint InvalidPropertyTypeCounter { get; private set; } = 0;
//        public void InvalidPropertyType()
//        {
//            InvalidPropertyTypeCounter++;
//            _inputLogger.InvalidPropertyType();
//        }
//        public uint InvalidPropertyValueCounter { get; private set; } = 0;
//        public void InvalidPropertyValue( PropertyIdentifier requestResponseInformation, byte val1 )
//        {
//            InvalidPropertyValueCounter++;
//            _inputLogger.InvalidPropertyValue( requestResponseInformation, val1 );
//        }
//        public uint LoopCanceledExceptionCounter { get; private set; } = 0;
//        public void LoopCanceledException( Exception e )
//        {
//            LoopCanceledExceptionCounter++;
//            _inputLogger.LoopCanceledException( e );
//        }
//        public uint PacketMarkedAsDroppedCounter { get; private set; } = 0;
//        public void PacketMarkedAsDropped( ushort packetId )
//        {
//            PacketMarkedAsDroppedCounter++;
//            _inputLogger.PacketMarkedAsDropped( packetId );
//        }
//        public int ProcessPacketCounter { get; private set; } = 0;
//        public IDisposable? ProcessPacket( PacketType packetType )
//        {
//            ProcessPacketCounter++;
//            return _inputLogger.ProcessPacket( packetType );
//        }
//        public uint ProcessPublishPacketCounter { get; private set; } = 0;
//        public IDisposable? ProcessPublishPacket( InputPump sender, byte header, uint packetLength, PipeReader reader, Func<ValueTask<OperationStatus>> next, QualityOfService qos )
//        {
//            ProcessPublishPacketCounter++;
//            return _inputLogger.ProcessPublishPacket( sender, header, packetLength, reader, next, qos );
//        }
//        public uint QueueFullPacketDroppedCounter { get; private set; } = 0;
//        public void QueueFullPacketDropped( PacketType packetType, ushort packetId )
//        {
//            QueueFullPacketDroppedCounter++;
//            _inputLogger.QueueFullPacketDropped( packetType, packetId );
//        }
//        public uint ReadCancelledCounter { get; private set; } = 0;
//        public void ReadCancelled( uint requestedByteCount )
//        {
//            ReadCancelledCounter++;
//            _inputLogger.ReadCancelled( requestedByteCount );
//        }
//        public uint ReadLoopTokenCancelledCounter { get; private set; } = 0;
//        public void ReadLoopTokenCancelled()
//        {
//            ReadLoopTokenCancelledCounter++;
//            _inputLogger.ReadLoopTokenCancelled();
//        }
//        public uint RentingBytesStoreCounter { get; private set; } = 0;
//        public void RentingBytesStore( uint packetSize, IOutgoingPacket packet )
//        {
//            RentingBytesStoreCounter++;
//            _inputLogger.RentingBytesStore( packetSize, packet );
//        }
//        public int SerializingPacketInMemoryCounter { get; private set; } = 0;
//        public IDisposable? SerializingPacketInMemory( IOutgoingPacket packet )
//        {
//            SerializingPacketInMemoryCounter++;
//            return _inputLogger.SerializingPacketInMemory( packet );
//        }
//        public uint UncertainPacketFreedCounter { get; private set; } = 0;
//        public void UncertainPacketFreed( ushort PacketId )
//        {
//            UncertainPacketFreedCounter++;
//            _inputLogger.UncertainPacketFreed( packetId );
//        }
//        /// <summary>
//        /// UnexpectedEndOfStream()
//        /// </summary>
//        public uint UnexpectedEndOfStreamCounter1 { get; private set; } = 0;
//        public void UnexpectedEndOfStream()
//        {
//            UnexpectedEndOfStreamCounter1++;
//            _inputLogger.UnexpectedEndOfStream();
//        }
//        /// <summary>
//        /// UnexpectedEndOfStream( int requestedByteCount, int availableByteCount )
//        /// </summary>
//        public uint UnexpectedEndOfStreamCounter2 { get; private set; } = 0;
//        public void UnexpectedEndOfStream( uint requestedByteCount, uint availableByteCount )
//        {
//            UnexpectedEndOfStreamCounter2++;
//            _inputLogger.UnexpectedEndOfStream( requestedByteCount, availableByteCount );
//        }
//        public uint UnparsedExtraBytesCounter { get; private set; } = 0;
//        public void UnparsedExtraBytes( InputPump incomingMessageHandler, PacketType packetType, byte header, uint packetSize, uint unparsedSize )
//        {
//            UnparsedExtraBytesCounter++;
//            _inputLogger.UnparsedExtraBytes( incomingMessageHandler, packetType, header, packetSize, unparsedSize );
//        }
//        public uint UnparsedExtraBytesPacketIdCounter { get; private set; } = 0;
//        public void UnparsedExtraBytesPacketId( uint unparsedSize )
//        {
//            UnparsedExtraBytesPacketIdCounter++;
//            _inputLogger.UnparsedExtraBytesPacketId( unparsedSize );
//        }

//        public uint ProtocolViolationCounter { get; private set; } = 0;
//        public void ProtocolViolation( ProtocolViolationException e )
//        {
//            ProtocolViolationCounter++;
//            _inputLogger.ProtocolViolation( e );
//        }

//        public uint ReflexSignaledInvalidDataCounter { get; private set; } = 0;

//        public IActivityMonitor? Monitor => _inputLogger.Monitor;

//        public void ReflexSignaledInvalidData()
//        {
//            ReflexSignaledInvalidDataCounter++;
//            _inputLogger.ReflexSignaledInvalidData();
//        }
//    }
//}
