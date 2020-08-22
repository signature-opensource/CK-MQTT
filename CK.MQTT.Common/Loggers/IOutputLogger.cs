using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public interface IOutputLogger
    {
        IDisposable? OutputLoopStarting();
        void ExceptionInOutputLoop(Exception e); 
        IDisposable? SendingMessage(ref IOutgoingPacket outgoingPacket );
        void PacketMarkedPoisoned( int packetId, int tryCount );
    }
}
