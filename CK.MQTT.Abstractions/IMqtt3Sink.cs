using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public interface IMqtt3Sink
    {
        ValueTask ReceiveAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken );

        public enum ManualConnectRetryBehavior
        {
            GiveUp,
            Retry,
            YieldToBackground
        }

        ManualConnectRetryBehavior OnFailedManualConnect( ConnectResult connectResult );

        /// <returns><see langword="true"/>to keep reconnecting.</returns>
        bool OnUnattendedDisconnect( DisconnectReason reason );

        /// <returns><see langword="true"/> to try reconnecting. You can add delay logic to temporise a reconnection.</returns>
        ValueTask<bool> OnReconnectionFailedAsync( ConnectResult result );

        /// <summary>
        /// Called when the client is successfuly connected.
        /// </summary>
        void Connected();

        void OnPoisonousPacket( ushort packetId, PacketType packetType, int poisonousTotalCount );

        void OnPacketResent( ushort packetId, int packetInTransitOrLost, bool isDropped );

        void OnQueueFullPacketDropped( ushort packetId );
        void OnQueueFullPacketDropped( ushort packetId, PacketType packetType );

        void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData );
        void OnPacketWithDupFlagReceived( PacketType packetType );
    }
}
