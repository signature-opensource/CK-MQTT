using CK.MQTT.Client;
using CK.MQTT.Packets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Stores
{
    public interface ILocalPacketStore : IDisposable
    {
        bool IsRevivedSession { get; set; }

        [ThreadColor( ThreadColor.Rainbow )]
        ValueTask<(Task<object?> ackTask, IOutgoingPacket packetToSend)> StoreMessageAsync( IOutgoingPacket packet, QualityOfService qos );
        void CancelAllAckTask();

        [ThreadColor( "WriteLoop" )]
        void OnPacketSent( ushort PacketId );

        [ThreadColor( "ReadLoop" )]
        ValueTask OnQos1AckAsync( IMqtt3Sink sink, ushort PacketId, object? result );

        /// <returns>The lifecycle packet to send.</returns>
        [ThreadColor( "ReadLoop" )]
        ValueTask<IOutgoingPacket> OnQos2AckStep1Async( ushort PacketId );

        [ThreadColor( "ReadLoop" )]
        void OnQos2AckStep2( ushort PacketId );

        [ThreadColor( "WriteLoop" )]
        ValueTask<(IOutgoingPacket? outgoingPacket, TimeSpan timeUntilAnotherRetry)> GetPacketToResendAsync();
        CancellationToken DroppedPacketCancelToken { get; }
        ValueTask ResetAsync();

        [ThreadColor( "ReadLoop" )]
        void BeforeQueueReflexPacket( Action<IOutgoingPacket> queuePacket, IOutgoingPacket outgoingPacket );
    }
}