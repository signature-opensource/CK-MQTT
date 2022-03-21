//using CK.Core;
//using System;
//using System.Threading;
//using System.Threading.Tasks;

//namespace CK.MQTT
//{
//    /// <summary>
//    /// Simple wrapper of an <see cref="IActivityMonitor"/> to a <see cref="IOutputLogger"/>.
//    /// </summary>
//    public class OutputLoggerMqttActivityMonitor : IOutputLogger
//    {
//        public IActivityMonitor Monitor { get; }

//        /// <summary>
//        /// Instantiate this wrapper.
//        /// </summary>
//        /// <param name="m">The <see cref="IActivityMonitor"/> to wrap.</param>
//        public OutputLoggerMqttActivityMonitor( IActivityMonitor m )
//        {
//            Monitor = m;
//        }

//        public void AwaitCompletedDueTo( IDisposableGroup? grp,
//            Task<bool> reflexesWait, Task<bool> messagesWait,
//            CancellationToken packetResend, CancellationToken cancelOnPacketDropped, CancellationToken cancellationToken )
//        {
//            if( cancellationToken.IsCancellationRequested )
//            {
//                grp?.ConcludeWith( () => "Given CancellationToken has been cancelled." );
//            }
//            if( cancelOnPacketDropped.IsCancellationRequested )
//            {
//                grp?.ConcludeWith( () => "A packet has been dropped." );
//            }
//            if( packetResend.IsCancellationRequested )
//            {
//                grp?.ConcludeWith( () => "We should resend a packet." );
//            }
//            if( messagesWait.IsCompletedSuccessfully )
//            {
//                grp?.ConcludeWith( () => "A message is available to send." );
//            }
//            if( reflexesWait.IsCompletedSuccessfully )
//            {
//                grp?.ConcludeWith( () => "A reflex message is available to send." );
//            }
//        }

//        public IDisposableGroup AwaitingWork() => Monitor.OpenTrace( "Awaiting that some work is available before restarting." );


//        public void ConcludeTimeUntilNextUnackRetry( IDisposableGroup disposableGroup, TimeSpan timeUntilAnotherRetry )
//            => _ = timeUntilAnotherRetry == Timeout.InfiniteTimeSpan ?
//                disposableGroup.ConcludeWith( () => $"Done resending unacks packets, no scheduled retries." ) :
//                disposableGroup.ConcludeWith( () => $"Done resending unacks packets, next retry in {timeUntilAnotherRetry}." );

//        public void ExceptionInOutputLoop( Exception e ) => Monitor.Error( "Error while writing data.", e );

//        public IDisposable MainLoopSendingKeepAlive() => Monitor.OpenTrace( "Sending keep alive..." );

//        public void NoUnackPacketSent( TimeSpan timeUntilAnotherRetry )
//        {
//            if( timeUntilAnotherRetry == Timeout.InfiniteTimeSpan )
//            {
//                Monitor.Trace( "There is no unack packet." );
//            }
//            else
//            {
//                Monitor.Trace( $"No UnAck packet sent, next retry scheduled in {timeUntilAnotherRetry}." );
//            }
//        }

//        public void OutputLoopCancelled( OperationCanceledException e )
//            => Monitor.Trace( "Output loop cancelled.", e );

//        public IDisposable? OutputLoopStarting() => Monitor.OpenTrace( "Output loop listening..." );

//        public IDisposable OutputProcessorRunning() => Monitor.OpenTrace( "Output processor running..." );

//        public void QueueEmpty() => Monitor.Trace( "Queues are empty." );

//        public IDisposableGroup ResendAllUnackPacket()
//            => Monitor.OpenTrace( "Resending all unack packets..." );

//        public IDisposable? SendingMessage( ref IOutgoingPacket outgoingPacket, ProtocolLevel protocolLevel )
//            => outgoingPacket.Qos == QualityOfService.AtMostOnce
//                ? Monitor.OpenInfo( $"Sending message '{outgoingPacket}' of size {outgoingPacket.GetSize( protocolLevel )}." )
//                : Monitor.OpenInfo( $"Sending message '{outgoingPacket}' of size {outgoingPacket.GetSize( protocolLevel )} with QoS {outgoingPacket.Qos} and packet ID {outgoingPacket.PacketId}." );

//        public IDisposable SendingMessageFromQueue()
//            => Monitor.OpenTrace( "Sending a message from queue." );
//    }
//}
