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

        public void AwaitCompletedDueTo( IDisposableGroup? grp,
            Task<bool> reflexesWait, Task<bool> messagesWait,
            CancellationToken packetResend, CancellationToken cancelOnPacketDropped, CancellationToken cancellationToken )
        {
            if( cancellationToken.IsCancellationRequested )
            {
                grp?.ConcludeWith( () => "Given CancellationToken has been cancelled." );
            }
            if( cancelOnPacketDropped.IsCancellationRequested )
            {
                grp?.ConcludeWith( () => "A packet has been dropped." );
            }
            if( packetResend.IsCancellationRequested )
            {
                grp?.ConcludeWith( () => "We should resend a packet." );
            }
            if( messagesWait.IsCompletedSuccessfully )
            {
                grp?.ConcludeWith( () => "A message is available to send." );
            }
            if( reflexesWait.IsCompletedSuccessfully )
            {
                grp?.ConcludeWith( () => "A reflex message is available to send." );
            }
        }

        public IDisposableGroup AwaitingWork() => _m.OpenTrace( "Awaiting that some work is available before restarting." );


        public void ConcludeTimeUntilNextUnackRetry( IDisposableGroup disposableGroup, TimeSpan timeUntilAnotherRetry )
            => _ = timeUntilAnotherRetry == Timeout.InfiniteTimeSpan ?
                disposableGroup.ConcludeWith( () => $"Done resending unacks packets, no scheduled retries." ) :
                disposableGroup.ConcludeWith( () => $"Done resending unacks packets, next retry in {timeUntilAnotherRetry}." );

        public void ExceptionInOutputLoop( Exception e ) => _m.Error( "Error while writing data.", e );

        public IDisposable MainLoopSendingKeepAlive() => _m.OpenTrace( "Sending keep alive..." );

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

        public void OutputLoopCancelled( OperationCanceledException e )
            => _m.Trace( "Output loop cancelled.", e );

        public IDisposable? OutputLoopStarting() => _m.OpenTrace( "Output loop listening..." );

        public IDisposable OutputProcessorRunning() => _m.OpenTrace( "Output processor running..." );

        public void QueueEmpty() => _m.Trace( "Queues are empty." );

        public IDisposableGroup ResendAllUnackPacket()
            => _m.OpenTrace( "Resending all unack packets..." );

        public IDisposable? SendingMessage( ref IOutgoingPacket outgoingPacket, ProtocolLevel protocolLevel )
            => outgoingPacket.Qos == QualityOfService.AtMostOnce
                ? _m.OpenInfo( $"Sending message '{outgoingPacket}' of size {outgoingPacket.GetSize( protocolLevel )}." )
                : _m.OpenInfo( $"Sending message '{outgoingPacket}' of size {outgoingPacket.GetSize( protocolLevel )} with QoS {outgoingPacket.Qos} and packet ID {outgoingPacket.PacketId}." );

        public IDisposable SendingMessageFromQueue()
            => _m.OpenTrace( "Sending a message from queue." );
    }
}
