using CK.MQTT.Packets;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public abstract class Mqtt3SinkWrapper : IMqtt3Sink
    {
        protected readonly IMqtt3Sink _sink;

        public Mqtt3SinkWrapper( IMqtt3Sink sink ) => _sink = sink;

        public virtual IConnectedMessageSender Sender { get; set; } = null!; //Set by the client.

        public virtual void OnPacketResent( ushort packetId, ulong packetInTransitOrLost, bool isDropped ) => _sink.OnPacketResent( packetId, packetInTransitOrLost, isDropped );

        public virtual void OnPacketWithDupFlagReceived( PacketType packetType ) => _sink.OnPacketWithDupFlagReceived( packetType );

        public virtual void OnQueueFullPacketDropped( ushort packetId ) => _sink.OnQueueFullPacketDropped( packetId );

        public virtual void OnQueueFullPacketDropped( ushort packetId, PacketType packetType ) => _sink.OnQueueFullPacketDropped( packetId, packetType );

        public virtual bool OnUnattendedDisconnect( DisconnectReason reason ) => _sink.OnUnattendedDisconnect( reason );

        public virtual void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData ) => _sink.OnUnparsedExtraData( packetId, unparsedData );

        public virtual ValueTask ReceiveAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken ) => _sink.ReceiveAsync( topic, reader, size, q, retain, cancellationToken );
    }
}
