//using CK.Core;
//using CK.MQTT.Pumps;
//using System;
//using System.Buffers;
//using System.IO.Pipelines;
//using System.Net;
//using System.Threading.Tasks;

//namespace CK.MQTT
//{
//    /// <summary>
//    /// Simple wrapper of an <see cref="IActivityMonitor"/> to a <see cref="IInputLogger"/>.
//    /// </summary>
//    public class InputLoggerMqttActivityMonitor : IInputLogger
//    {
//        public IActivityMonitor Monitor { get; }

//        /// <summary>
//        /// Instantiate this wrapper.
//        /// </summary>
//        /// <param name="m">The <see cref="IActivityMonitor"/> to wrap.</param>
//        public InputLoggerMqttActivityMonitor( IActivityMonitor m ) => Monitor = m;

//        /// <inheritdoc/>
//        public IDisposable? ProcessPacket( PacketType packetType ) => Monitor.OpenTrace( $"Handling incoming packet as {packetType}." );

//        /// <inheritdoc/>
//        public IDisposable? ProcessPublishPacket( InputPump sender, byte header, uint packetLength, PipeReader reader, Func<ValueTask<OperationStatus>> next, QualityOfService qos )
//            => Monitor.OpenDebug( $"Handling incoming packet as {PacketType.Publish}." );

//        /// <inheritdoc/>
//        public void QueueFullPacketDropped( PacketType packetType, ushort PacketId )
//            => Monitor.Warn( $"Could not queue {packetType}. Message Queue is full !!!" );

//        /// <inheritdoc/>
//        public void ReadCancelled( uint requestedByteCount )
//            => Monitor.Trace( $"Read operation canceled while trying to read {requestedByteCount} bytes." );

//        /// <inheritdoc/>
//        public void UnexpectedEndOfStream( uint requestedByteCount, uint availableByteCount )
//            => Monitor.Error( $"Unexpected End Of Stream. Expected {requestedByteCount} bytes but got {availableByteCount}." );

//        /// <inheritdoc/>
//        public void UnparsedExtraBytes( InputPump incomingMessageHandler, PacketType packetType, byte header, uint packetSize, uint unparsedSize )
//            => Monitor.Warn( $"Packet bigger than expected, skipping {unparsedSize} bytes." );

//        /// <inheritdoc/>
//        public void UnparsedExtraBytesPacketId( uint unparsedSize )
//            => Monitor.Warn( $"Packet bigger than expected, skipping {unparsedSize} bytes." );

//        /// <inheritdoc/>
//        public IDisposable InputLoopStarting() => Monitor.OpenInfo( "Listening Incoming Messages..." );

//        /// <inheritdoc/>
//        public void ReadLoopTokenCancelled() => Monitor.Trace( "Read Loop Canceled." );

//        /// <inheritdoc/>
//        public void InvalidIncomingData() => Monitor.Error( "Invalid Incoming data." );

//        /// <inheritdoc/>
//        public void ExceptionOnParsingIncomingData( Exception e ) => Monitor.Error( "Error while parsing incoming data.", e );

//        /// <inheritdoc/>
//        public IDisposable? IncomingPacket( byte header, uint length ) => Monitor.OpenTrace( $"Incoming packet of {length} bytes." );

//        /// <inheritdoc/>
//        public void EndOfStream() => Monitor.Trace( "End of server Stream." );

//        /// <inheritdoc/>
//        public void UnexpectedEndOfStream() => Monitor.Error( "Unexpected End of Stream." );

//        /// <inheritdoc/>
//        public void LoopCanceledException( Exception e ) => Monitor.Trace( "Canceled exception in loop." );

//        /// <inheritdoc/>
//        public void FreedPacketId( ushort PacketId ) => Monitor.Trace( $"Freed packet id {packetId}." );

//        public void ConnectionUnknownException( Exception e ) => Monitor.Fatal( e );

//        public void PacketMarkedAsDropped( uint id ) => Monitor.Warn( $"Packet with ID {id} has been marked as dropped." );

//        public void UncertainPacketFreed( ushort PacketId ) => Monitor?.Trace( $"Uncertain packet ID {packetId} has been freed." );

//        public void RentingBytesStore( uint packetSize, IOutgoingPacket packet )
//            => Monitor?.Trace( $"Renting {packetSize} to persist {packet} in memory." );

//        public IDisposable? SerializingPacketInMemory( IOutgoingPacket packet )
//            => Monitor?.OpenTrace( $"Serializing {packet} in memory." );

//        public void ConnectPropertyFieldDuplicated( PropertyIdentifier propertyIdentifier )
//            => Monitor?.Error( $"The property {propertyIdentifier} is present more than once." );

//        public void InvalidPropertyValue( PropertyIdentifier propertyIdentifier, byte val1 )
//            => Monitor?.Error( $"The property {propertyIdentifier} has an invalid value {val1}." );

//        public void InvalidMaxPacketSize( uint maxPacketSize )
//            => Monitor?.Error( "Received packet exceed max packet size." );

//        public void InvalidPropertyType()
//            => Monitor.Error( "Received a packet with an unknown property type." );

//        public void ErrorAuthDataMissing()
//            => Monitor.Error( "Auth data is missing." );

//        public void ProtocolViolation( ProtocolViolationException e )
//            => Monitor.Fatal( "Protocol Violation", e );

//        public void ReflexSignaledInvalidData()
//            => Monitor.Fatal( "Invalid data or protocol error." );
//    }
//}
