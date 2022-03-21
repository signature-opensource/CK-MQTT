using CK.MQTT.Client;
using CK.MQTT.Packets;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Stores
{
    public interface ILocalPacketStore : IDisposable
    {
        bool IsRevivedSession { get; set; }
        ValueTask<(Task<object?> ackTask, IOutgoingPacket packetToSend)> StoreMessageAsync( IOutgoingPacket packet, QualityOfService qos );
        void CancelAllAckTask();
        void OnPacketSent( ushort PacketId );
        ValueTask OnQos1AckAsync( IMqtt3Sink sink, ushort PacketId, object? result );
        /// <returns>The lifecycle packet to send.</returns>
        ValueTask<IOutgoingPacket> OnQos2AckStep1Async( ushort PacketId );
        void OnQos2AckStep2( ushort PacketId );
        ValueTask<(IOutgoingPacket? outgoingPacket, TimeSpan timeUntilAnotherRetry)> GetPacketToResendAsync();
        CancellationToken DroppedPacketCancelToken { get; }
        ValueTask ResetAsync();

        void BeforeQueueReflexPacket( Action<IOutgoingPacket> queuePacket, IOutgoingPacket outgoingPacket );
    }
}
