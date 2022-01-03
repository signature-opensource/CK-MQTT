using CK.Core;
using CK.MQTT.Pumps;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Simple wrapper of an <see cref="IActivityMonitor"/> to a <see cref="IInputLogger"/>.
    /// </summary>
    public class InputLoggerMqttActivityMonitor : IInputLogger
    {
        readonly IActivityMonitor _m;

        /// <summary>
        /// Instantiate this wrapper.
        /// </summary>
        /// <param name="m">The <see cref="IActivityMonitor"/> to wrap.</param>
        public InputLoggerMqttActivityMonitor( IActivityMonitor m ) => _m = m;

        /// <inheritdoc/>
        public IDisposable? ProcessPacket( PacketType packetType ) => _m.OpenTrace( $"Handling incoming packet as {packetType}." );

        /// <inheritdoc/>
        public IDisposable? ProcessPublishPacket( InputPump sender, byte header, uint packetLength, PipeReader reader, Func<ValueTask<OperationStatus>> next, QualityOfService qos )
            => _m.OpenDebug( $"Handling incoming packet as {PacketType.Publish}." );

        /// <inheritdoc/>
        public void QueueFullPacketDropped( PacketType packetType, uint packetId )
            => _m.Warn( $"Could not queue {packetType}. Message Queue is full !!!" );

        /// <inheritdoc/>
        public void ReadCancelled( uint requestedByteCount )
            => _m.Trace( $"Read operation canceled while trying to read {requestedByteCount} bytes." );

        /// <inheritdoc/>
        public void UnexpectedEndOfStream( uint requestedByteCount, uint availableByteCount )
            => _m.Error( $"Unexpected End Of Stream. Expected {requestedByteCount} bytes but got {availableByteCount}." );

        /// <inheritdoc/>
        public void UnparsedExtraBytes( InputPump incomingMessageHandler, PacketType packetType, byte header, uint packetSize, uint unparsedSize )
            => _m.Warn( $"Packet bigger than expected, skipping {unparsedSize} bytes." );

        /// <inheritdoc/>
        public void UnparsedExtraBytesPacketId( uint unparsedSize )
            => _m.Warn( $"Packet bigger than expected, skipping {unparsedSize} bytes." );

        /// <inheritdoc/>
        public IDisposable InputLoopStarting() => _m.OpenInfo( "Listening Incoming Messages..." );

        /// <inheritdoc/>
        public void ReadLoopTokenCancelled() => _m.Trace( "Read Loop Canceled." );

        /// <inheritdoc/>
        public void InvalidIncomingData() => _m.Error( "Invalid Incoming data." );

        /// <inheritdoc/>
        public void ExceptionOnParsingIncomingData( Exception e ) => _m.Error( "Error while parsing incoming data.", e );

        /// <inheritdoc/>
        public IDisposable? IncomingPacket( byte header, uint length ) => _m.OpenTrace( $"Incoming packet of {length} bytes." );

        /// <inheritdoc/>
        public void EndOfStream() => _m.Trace( "End of server Stream." );

        /// <inheritdoc/>
        public void UnexpectedEndOfStream() => _m.Error( "Unexpected End of Stream." );

        /// <inheritdoc/>
        public void LoopCanceledException( Exception e ) => _m.Trace( "Canceled exception in loop." );

        /// <inheritdoc/>
        public void FreedPacketId( uint packetId ) => _m.Trace( $"Freed packet id {packetId}." );

        public void ConnectionUnknownException( Exception e ) => _m.Fatal( e );

        public void PacketMarkedAsDropped( uint id ) => _m.Warn( $"Packet with ID {id} has been marked as dropped." );

        public void UncertainPacketFreed( uint packetId ) => _m?.Trace( $"Uncertain packet ID {packetId} has been freed." );

        public void RentingBytesStore( uint packetSize, IOutgoingPacket packet )
            => _m?.Trace( $"Renting {packetSize} to persist {packet} in memory." );

        public IDisposable? SerializingPacketInMemory( IOutgoingPacket packet )
            => _m?.OpenTrace( $"Serializing {packet} in memory." );

        public void ConnectPropertyFieldDuplicated( PropertyIdentifier propertyIdentifier )
            => _m?.Error( $"The property {propertyIdentifier} is present more than once." );

        public void InvalidPropertyValue( PropertyIdentifier propertyIdentifier, byte val1 )
            => _m?.Error( $"The property {propertyIdentifier} has an invalid value {val1}." );

        public void InvalidMaxPacketSize( uint maxPacketSize )
            => _m?.Error( "Received packet exceed max packet size." );

        public void InvalidPropertyType()
            => _m.Error( "Received a packet with an unknown property type." );

        public void ErrorAuthDataMissing()
            => _m.Error( "Auth data is missing." );

        public void ProtocolViolation( ProtocolViolationException e )
            => _m.Fatal( "Protocol Violation", e );

        public void ReflexSignaledInvalidData()
            => _m.Fatal( "Invalid data or protocol error." );
    }
}
