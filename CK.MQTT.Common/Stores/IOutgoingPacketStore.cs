using CK.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Stores
{
    public interface IOutgoingPacketStore
    {
        ValueTask<(Task<object?> ackTask, IOutgoingPacket packetToSend)> StoreMessageAsync( IActivityMonitor? m, IOutgoingPacket packet, QualityOfService qos );
        void CancelAllAckTask( IActivityMonitor? m );
        void OnPacketSent( IOutputLogger? m, int packetId );
        ValueTask OnQos1AckAsync( IInputLogger? m, int packetId, object? result );
        /// <returns>The lifecycle packet to send.</returns>
        ValueTask<IOutgoingPacket> OnQos2AckStep1Async( IInputLogger? m, int packetId );
        void OnQos2AckStep2( IInputLogger? m, int packetId );
        ValueTask<(IOutgoingPacket? outgoingPacket, TimeSpan timeUntilAnotherRetry)> GetPacketToResendAsync();
        CancellationToken DroppedPacketCancelToken { get; }
        ValueTask ResetAsync();

        void BeforeQueueReflexPacket( IInputLogger? m, Action<IOutgoingPacket> queuePacket, IOutgoingPacket outgoingPacket );
    }
}
