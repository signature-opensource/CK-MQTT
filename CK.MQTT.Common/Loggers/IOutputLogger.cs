using CK.Core;
using System;

namespace CK.MQTT
{
    public interface IOutputLogger
    {
        IDisposable? OutputLoopStarting();
        void ExceptionInOutputLoop( Exception e );
        IDisposable? SendingMessage( ref IOutgoingPacket outgoingPacket, ProtocolLevel protocolLevel );
        IDisposable? SendingMessageWithId( ref IOutgoingPacket outgoingPacket, ProtocolLevel protocolLevel, int packetId );
        void PacketMarkedPoisoned( int packetId, int tryCount );
        void SendingKeepAlive();
        IDisposable SendingMessageFromQueue();
        IDisposableGroup ResendAllUnackPacket();
        void ConcludeTimeUntilNextUnackRetry( IDisposableGroup disposableGroup, TimeSpan timeUntilAnotherRetry );
        IDisposableGroup MainOutputProcessorLoop();
        void ConcludeMainLoopTimeout( IDisposableGroup disposableGroup );
        void ConcludeRegularPacketSent( IDisposableGroup disposableGroup );
        void ConcludeMessageInQueueAvailable( IDisposableGroup disposableGroup );
        void ConcludeMainLoopCancelled( IDisposableGroup disposableGroup );
        void ConcludePacketDroppedAvailable( IDisposableGroup disposableGroup );
        IDisposable MainLoopSendingKeepAlive();
        void ConcludeSentKeepAlive( IDisposableGroup disposableGroup );
        void QueueEmpty();
        void NoUnackPacketSent( TimeSpan timeUntilAnotherRetry );
        void AwaitingWork();
    }
}
