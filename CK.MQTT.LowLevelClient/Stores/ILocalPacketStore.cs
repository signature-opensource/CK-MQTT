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

        /// <returns><see langword="true"/> if a packet has been dropped.</returns>
        [ThreadColor( "ReadLoop" )]
        bool OnQos1Ack( IMqtt3Sink sink, ushort PacketId, object? result );

        /// <returns>The lifecycle packet to send.</returns>
        [ThreadColor( "ReadLoop" )]
        ValueTask<IOutgoingPacket> OnQos2AckStep1Async( ushort PacketId );

        [ThreadColor( "ReadLoop" )]
        void OnQos2AckStep2( ushort PacketId );

        [ThreadColor( "WriteLoop" )]
        ValueTask<(IOutgoingPacket? outgoingPacket, TimeSpan timeUntilAnotherRetry)> GetPacketToResendAsync();
        ValueTask ResetAsync();

    }
}
