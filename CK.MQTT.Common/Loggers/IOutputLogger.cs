//using CK.Core;
//using System;
//using System.Threading;
//using System.Threading.Tasks;

//namespace CK.MQTT
//{
//    public interface IOutputLogger2
//    {
//        IDisposable? OutputLoopStarting();
//        void ExceptionInOutputLoop( Exception e );
//        IDisposable? SendingMessage( ref IOutgoingPacket outgoingPacket, ProtocolLevel protocolLevel );
//        IDisposable SendingMessageFromQueue();
//        IDisposableGroup ResendAllUnackPacket();
//        void ConcludeTimeUntilNextUnackRetry( IDisposableGroup disposableGroup, TimeSpan timeUntilAnotherRetry );
//        IDisposable OutputProcessorRunning();
//        IDisposable MainLoopSendingKeepAlive();
//        void QueueEmpty();
//        void NoUnackPacketSent( TimeSpan timeUntilAnotherRetry );
//        IDisposableGroup AwaitingWork();
//        void AwaitCompletedDueTo( IDisposableGroup? grp, Task<bool> reflexesWait, Task<bool> messagesWait, CancellationToken packetResend, CancellationToken cancelOnPacketDropped, CancellationToken cancellationToken );
//        void OutputLoopCancelled( OperationCanceledException e );

//        IActivityMonitor Monitor { get; }
//    }
//}
