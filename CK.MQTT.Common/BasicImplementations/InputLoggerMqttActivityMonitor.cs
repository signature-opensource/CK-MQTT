using CK.Core;
using CK.MQTT.Pumps;
using System;
using System.IO.Pipelines;
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
        public void PingReqTimeout() => _m.Error( "The broker did not responded PingReq in the given amount of time." );

        /// <inheritdoc/>
        public void InvalidDataReceived( DisconnectedReason reason ) => _m.Info( $"Client closing reason: '{reason}.'" );

        /// <inheritdoc/>
        public void DoubleFreePacketId( int packetId )
            => _m.Error( $"Freeing packet id {packetId} that was not assigned or already freed." );

        /// <inheritdoc/>
        public IDisposable? ProcessPacket( PacketType packetType ) => _m.OpenTrace( $"Handling incoming packet as {packetType}." );

        /// <inheritdoc/>
        public IDisposable? ProcessPublishPacket( InputPump sender, byte header, int packetLength, PipeReader reader, Func<ValueTask> next, QualityOfService qos )
            => _m.OpenDebug( $"Handling incoming packet as {PacketType.Publish}." );

        /// <inheritdoc/>
        public void QueueFullPacketDropped( PacketType packetType, int packetId )
            => _m.Warn( $"Could not queue {packetType}. Message Queue is full !!!" );

        /// <inheritdoc/>
        public void ReadCancelled( int requestedByteCount )
            => _m.Trace( $"Read operation canceled while trying to read {requestedByteCount} bytes." );

        /// <inheritdoc/>
        public void UnexpectedEndOfStream( int requestedByteCount, int availableByteCount )
            => _m.Error( $"Unexpected End Of Stream. Expected {requestedByteCount} bytes but got {availableByteCount}." );

        /// <inheritdoc/>
        public void UnparsedExtraBytes( InputPump incomingMessageHandler, PacketType packetType, byte header, int packetSize, int unparsedSize )
            => _m.Warn( $"Packet bigger than expected, skipping {unparsedSize} bytes." );

        /// <inheritdoc/>
        public void UnparsedExtraBytesPacketId( int unparsedSize )
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
        public IDisposable? ReflexTimeout() => _m.OpenError( "Timeout while waiting for a reflex packet." );

        /// <inheritdoc/>
        public IDisposable? IncomingPacket( byte header, int length ) => _m.OpenTrace( $"Incoming packet of {length} bytes." );

        /// <inheritdoc/>
        public void EndOfStream() => _m.Trace( "End of server Stream." );

        /// <inheritdoc/>
        public void UnexpectedEndOfStream() => _m.Error( "Unexpected End of Stream." );

        /// <inheritdoc/>
        public void LoopCanceledException( Exception e ) => _m.Trace( "Canceled exception in loop." );

        /// <inheritdoc/>
        public void FreedPacketId( int packetId ) => _m.Trace( $"Freed packet id {packetId}." );

        public void ConnectionUnknownException( Exception e ) => _m.Fatal( e );

        public void ConnectPropertyFieldDuplicated( PropertyIdentifier propertyIdentifier )
            => _m.Error( $"{propertyIdentifier} is included more than once." );

        public void InvalidMaxPacketSize( int maxPacketSize )
            => _m?.Error( $"Invalid Max Packet Size ({maxPacketSize})." );

        public void InvalidPropertyType()
            => _m?.Error( "Invalid property type." );

        public void InvalidPropertyValue( PropertyIdentifier propertyIdentifier, object value )
            => _m?.Error( $"{propertyIdentifier} has an invalid value ({value})." );

        public void ErrorAuthDataMissing() => _m?.Error( "Auth data present but there is no auth method." );

        public void PacketMarkedAsDropped( int id ) => _m.Warn( $"Packet with ID {id} has been marked as dropped." );

        public void UncertainPacketFreed( int packetId ) => _m?.Trace( $"Uncertain packet ID {packetId} has been freed." );
    }
}
