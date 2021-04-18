using CK.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

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

        public void AwaitCompletedDueTo( IDisposableGroup? disposableGroup, Task<bool> reflexesWait, Task<bool> messagesWait, Task packetMarkedAsDropped, Task timeToWaitForRetry )
            => disposableGroup?.ConcludeWith( () => $"Await completed due to " +
                $"{nameof( reflexesWait )}: {reflexesWait.IsCompleted}, " +
                $"{nameof( messagesWait )}: {messagesWait.IsCompleted}, " +
                $"{nameof( packetMarkedAsDropped )}: {packetMarkedAsDropped.IsCompleted}, " +
                $"{nameof( timeToWaitForRetry )}: {timeToWaitForRetry.IsCompleted}" );

        public IDisposableGroup AwaitingWork() => _m.OpenTrace( "Awaiting that some work is available before restarting." );

        public void ConcludeMainLoopCancelled( IDisposableGroup disposableGroup )
        { }// => disposableGroup.ConcludeWith( () => "Canceled." );

        public void ConcludeMainLoopTimeout( IDisposableGroup disposableGroup )
        { }//   => disposableGroup.ConcludeWith( () => "Timeout." );

        public void ConcludeMessageInQueueAvailable( IDisposableGroup disposableGroup )
        { }//   => disposableGroup.ConcludeWith( () => "Detected message in queue available." );

        public void ConcludePacketDroppedAvailable( IDisposableGroup disposableGroup )
        { }//    => disposableGroup.ConcludeWith( () => "Detected dropped message to resend." );

        public void ConcludeRegularPacketSent( IDisposableGroup disposableGroup )
        { }//   => disposableGroup.ConcludeWith( () => "A packet has been sent." );

        public void ConcludeSentKeepAlive( IDisposableGroup disposableGroup )
        { }//  => disposableGroup.ConcludeWith( () => "Sent KeepAlive." );

        public void ConcludeTimeUntilNextUnackRetry( IDisposableGroup disposableGroup, TimeSpan timeUntilAnotherRetry )
        { }//  => _ = timeUntilAnotherRetry == Timeout.InfiniteTimeSpan ?
           //disposableGroup.ConcludeWith( () => $"Done resending unacks packets, no scheduled retries." ) :
           //      disposableGroup.ConcludeWith( () => $"Done resending unacks packets, next retry in {timeUntilAnotherRetry}." );

        public void ExceptionInOutputLoop( Exception e ) { }//=> _m.Error( "Error while writing data.", e );

        public IDisposable MainLoopSendingKeepAlive() => null;//_m.OpenTrace( "Sending keep alive..." );

        public IDisposableGroup MainOutputProcessorLoop() => null;// _m.OpenTrace( "Main output process running..." );

        public void NoUnackPacketSent( TimeSpan timeUntilAnotherRetry )
        {
            return;
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

        public IDisposable OutputProcessorRunning() => _m.OpenTrace( "Output processor running..." );

        public void PacketMarkedPoisoned( int packetId, int tryCount )
            => _m.Error( $"Packet with id {packetId} is not acknowledged after sending it {tryCount} times." +
                         $"\nThis was the last attempt, as configured." );

        public void QueueEmpty() => _m.Trace( "Queues are empty." );

        public IDisposableGroup ResendAllUnackPacket()
            => _m.OpenTrace( "Resending all unack packets..." );

        public void SendingKeepAlive() => _m.Trace( "Sending PingReq." );

        public IDisposable? SendingMessage( ref IOutgoingPacket outgoingPacket, ProtocolLevel protocolLevel )
            => outgoingPacket.Qos == QualityOfService.AtMostOnce
                ? _m.OpenInfo( $"Sending message '{outgoingPacket}' of size {outgoingPacket.GetSize( protocolLevel )}." )
                : _m.OpenInfo( $"Sending message '{outgoingPacket}' of size {outgoingPacket.GetSize( protocolLevel )} with QoS {outgoingPacket.Qos} and packet ID {outgoingPacket.PacketId}." );

        public IDisposable SendingMessageFromQueue()
            => _m.OpenTrace( "Sending a message from queue." );
    }
}
