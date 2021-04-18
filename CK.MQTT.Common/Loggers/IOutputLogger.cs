using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IOutputLogger
    {
        IDisposable? OutputLoopStarting();
        void ExceptionInOutputLoop( Exception e );
        IDisposable? SendingMessage( ref IOutgoingPacket outgoingPacket, ProtocolLevel protocolLevel );
        IDisposable SendingMessageFromQueue();
        IDisposableGroup ResendAllUnackPacket();
        void ConcludeTimeUntilNextUnackRetry( IDisposableGroup disposableGroup, TimeSpan timeUntilAnotherRetry );
        IDisposable OutputProcessorRunning();
        IDisposable MainLoopSendingKeepAlive();
        void ConcludeSentKeepAlive( IDisposableGroup disposableGroup );
        void QueueEmpty();
        void NoUnackPacketSent( TimeSpan timeUntilAnotherRetry );
        IDisposableGroup AwaitingWork();
        void AwaitCompletedDueTo( IDisposableGroup? disposableGroup, Task<bool> reflexesWait, Task<bool> messagesWait, Task packetMarkedAsDropped, Task timeToWaitForRetry );
    }
}
