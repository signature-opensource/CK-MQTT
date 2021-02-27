using CK.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.MQTT.Stores
{
    public interface IMqttIdStore
    {
        ValueTask<(Task<object?> ackTask, IOutgoingPacket packetToSend)> StoreMessageAsync( IActivityMonitor? m, IOutgoingPacketWithId packet, QualityOfService qos );
        void CancelAllAckTask( IActivityMonitor? m );
        void OnPacketSent( IOutputLogger? m, int packetId );
        ValueTask OnQos1AckAsync( IInputLogger? m, int packetId, object? result );
        ValueTask OnQos2AckStep1Async( IInputLogger? m, int packetId );
        void OnQos2AckStep2( IInputLogger? m, int packetId );
        ValueTask<(IOutgoingPacket? outgoingPacket, TimeSpan timeUntilAnotherRetry)> GetPacketToResend();
        Task GetTaskResolvedOnPacketDropped();
        ValueTask ResetAsync();
    }
}
