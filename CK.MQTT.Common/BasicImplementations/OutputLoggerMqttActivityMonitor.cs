using CK.Core;
using System;
using System.Threading;

namespace CK.MQTT
{
    /// <summary>
    /// Simple wrapper of an <see cref="IActivityMonitor"/> to a <see cref="IOutputLogger"/>.
    /// </summary>
    public class OutputLoggerMqttActivityMonitor : IOutputLogger
    {
        readonly IActivityMonitor _m;

        /// <summary>
        /// Instantiate this wrapper.
        /// </summary>
        /// <param name="m">The <see cref="IActivityMonitor"/> to wrap.</param>
        public OutputLoggerMqttActivityMonitor( IActivityMonitor m ) => _m = m;

        public void AwaitingWork() => _m.Trace( "Awaiting that some work is available before restarting." );

        public void ConcludeMainLoopCancelled( IDisposableGroup disposableGroup )
            => disposableGroup.ConcludeWith( () => "Canceled." );

        public void ConcludeMainLoopTimeout( IDisposableGroup disposableGroup )
            => disposableGroup.ConcludeWith( () => "Timeout." );

        public void ConcludeMessageInQueueAvailable( IDisposableGroup disposableGroup )
            => disposableGroup.ConcludeWith( () => "Detected message in queue available." );

        public void ConcludePacketDroppedAvailable( IDisposableGroup disposableGroup )
            => disposableGroup.ConcludeWith( () => "Detected dropped message to resend." );

        public void ConcludeRegularPacketSent( IDisposableGroup disposableGroup )
            => disposableGroup.ConcludeWith( () => "A packet has been sent." );

        public void ConcludeSentKeepAlive( IDisposableGroup disposableGroup )
            => disposableGroup.ConcludeWith( () => "Sent KeepAlive." );

        public void ConcludeTimeUntilNextUnackRetry( IDisposableGroup disposableGroup, TimeSpan timeUntilAnotherRetry )
            => _ = timeUntilAnotherRetry == Timeout.InfiniteTimeSpan ?
                disposableGroup.ConcludeWith( () => $"Done resending unacks packets, no scheduled retries." ) :
                disposableGroup.ConcludeWith( () => $"Done resending unacks packets, next retry in {timeUntilAnotherRetry}." );

        public void ExceptionInOutputLoop( Exception e ) => _m.Error( "Error while writing data.", e );

        public IDisposable MainLoopSendingKeepAlive() => _m.OpenTrace( "Sending keep alive..." );

        public IDisposableGroup MainOutputProcessorLoop() => _m.OpenTrace( "Main output process running..." );

        public void NoUnackPacketSent( TimeSpan timeUntilAnotherRetry )
        {
            if( timeUntilAnotherRetry == Timeout.InfiniteTimeSpan )
            {
                _m.Trace( "There is no unack packet." );
            }
            else
            {
                _m.Trace( $"No UnAck packet sent, next retry scheduled in {timeUntilAnotherRetry}." );
            }
        }

        public IDisposable? OutputLoopStarting() => _m.OpenTrace( "Output loop listening..." );

        public void PacketMarkedPoisoned( int packetId, int tryCount )
            => _m.Error( $"Packet with id {packetId} is not acknowledged after sending it {tryCount} times." +
                         $"\nThis was the last attempt, as configured." );

        public void QueueEmpty() => _m.Trace( "Queues are empty." );

        public IDisposableGroup ResendAllUnackPacket()
            => _m.OpenTrace( "Resending all unack packets..." );

        public void SendingKeepAlive() => _m.Trace( "Sending PingReq." );

        public IDisposable? SendingMessage( ref IOutgoingPacket outgoingPacket, ProtocolLevel protocolLevel )
            => _m.OpenInfo( $"Sending message '{outgoingPacket}' of size {outgoingPacket.GetSize( protocolLevel )}." );

        public IDisposable SendingMessageFromQueue()
            => _m.OpenTrace( "Sending a message from queue." );

        public IDisposable? SendingMessageWithId( ref IOutgoingPacket outgoingPacket, ProtocolLevel protocolLevel, int packetId )
            => _m.OpenInfo( $"Sending message '{outgoingPacket}' of size {outgoingPacket.GetSize( protocolLevel )} with packet ID {packetId}." );
    }
}
