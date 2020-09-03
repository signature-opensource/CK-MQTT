using System;

namespace CK.MQTT
{
    public interface IOutputLogger
    {
        IDisposable? OutputLoopStarting();
        void ExceptionInOutputLoop(Exception e); 
        IDisposable? SendingMessage(ref IOutgoingPacket outgoingPacket, ProtocolLevel protocolLevel );
        void PacketMarkedPoisoned( int packetId, int tryCount );
    }
}
