using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public abstract class MQTT3SinkWrapper : IMQTT3Sink
    {
        protected readonly IMQTT3Sink _sink;

        public MQTT3SinkWrapper( IMQTT3Sink sink ) => _sink = sink;

        public virtual IConnectedMessageSender Sender { get; set; } = null!; //Set by the client.

        public virtual void OnPacketResent( ushort packetId, ulong packetInTransitOrLost, bool isDropped ) => _sink.OnPacketResent( packetId, packetInTransitOrLost, isDropped );

        public virtual void OnPacketWithDupFlagReceived( PacketType packetType ) => _sink.OnPacketWithDupFlagReceived( packetType );

        public virtual void OnQueueFullPacketDropped( ushort packetId ) => _sink.OnQueueFullPacketDropped( packetId );

        public virtual void OnQueueFullPacketDropped( ushort packetId, PacketType packetType ) => _sink.OnQueueFullPacketDropped( packetId, packetType );

        public virtual void OnUnattendedDisconnect( DisconnectReason reason ) => _sink.OnUnattendedDisconnect( reason );

        public virtual void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData ) => _sink.OnUnparsedExtraData( packetId, unparsedData );

        public virtual ValueTask OnMessageAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken ) => _sink.OnMessageAsync( topic, reader, size, q, retain, cancellationToken );
    }
}
