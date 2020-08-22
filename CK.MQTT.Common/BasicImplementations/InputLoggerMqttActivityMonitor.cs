using CK.Core;
using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Simple wrapper of an <see cref="IActivityMonitor"/> to a <see cref="IMqttLogger"/>.
    /// </summary>
    public class InputLoggerMqttActivityMonitor : IInputLogger
    {
        readonly IActivityMonitor _m;

        /// <summary>
        /// Instantiate this wrapper.
        /// </summary>
        /// <param name="m">The <see cref="IActivityMonitor"/> to wrap.</param>
        public InputLoggerMqttActivityMonitor( IActivityMonitor m )
        {
            _m = m;
        }

        public void PingReqTimeout() => _m.Error()?.Send( "The broker did not responded PingReq in the given amount of time." );

        public void ClientSelfClosing( DisconnectedReason reason ) => _m.Info()?.Send( $"Client closing reason: '{reason}.'" );

        public void DoubleFreePacketId( int packetId )
            => _m.Error()?.Send( $"Freeing packet id {packetId} that was not assigned or already freed." );

        public IDisposable? ProcessPacket( PacketType packetType ) => _m.OpenTrace()?.Send( $"Handling incoming packet as {packetType}." );

        public IDisposable? ProcessPublishPacket( IncomingMessageHandler sender, byte header, int packetLength, PipeReader reader, Func<ValueTask> next, QualityOfService qos )
            => _m.OpenTrace()?.Send( $"Handling incoming packet as {PacketType.Publish}." );

        public void QueueFullPacketDropped( PacketType packetType, int packetId )
            => _m.Warn()?.Send( $"Could not queue {packetType}. Message Queue is full !!!" );

        public void ReadCancelled( int requestedByteCount )
            => _m.Trace()?.Send( $"Read operation cancelled while trying to read {requestedByteCount} bytes." );

        public void UnexpectedEndOfStream( int requestedByteCount, int availableByteCount )
            => _m.Error().Send( $"Unexpected End Of Stream. Expected {requestedByteCount} bytes but got {availableByteCount}." );

        public void UnparsedExtraBytes( IncomingMessageHandler incomingMessageHandler, PacketType packetType, byte header, int packetSize, int unparsedSize )
            => _m.Warn().Send( $"Packet bigger than expected, skipping {unparsedSize} bytes." );

        public void UnparsedExtraBytesPacketId( int unparsedSize )
            => _m.Warn().Send( $"Packet bigger than expected, skipping {unparsedSize} bytes." );

        public IDisposable? InputLoopStarting() => _m.OpenTrace()?.Send( "Listening Incoming Messages..." );

        public void ReadLoopTokenCancelled() => _m.Trace()?.Send( "Read Loop Cancelled." );

        public void InvalidIncomingData() => _m.Error()?.Send( "Invalid Incoming data." );

        public void ExceptionOnParsingIncomingData( Exception e ) => _m.Error()?.Send( e, "Error while parsing incoming data." );

        public IDisposable? ReflexTimeout() => _m.OpenError()?.Send( "A reflex waited a packet too long and timeouted." );

        public IDisposable? IncomingPacket( byte header, int length ) => _m.OpenTrace()?.Send( $"Incoming packet of {length} bytes." );
        public void EndOfStream() => _m.Trace()?.Send( $"End of Stream." );

        public void UnexpectedEndOfStream() => _m.Error()?.Send( "Unexpected End of Stream." );

        public void LoopCanceledException( Exception e ) => _m.Trace()?.Send( e, "Cancelled exception in loop." );

        public void FreedPacketId( int packetId ) => _m.Trace()?.Send( $"Freed packet id {packetId}." );
    }
}
