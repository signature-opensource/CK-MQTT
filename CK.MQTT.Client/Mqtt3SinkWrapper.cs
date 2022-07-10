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
    public class Mqtt3SinkWrapper : IMqtt3Sink
    {
        readonly IMqtt3Sink _sink;

        public Mqtt3SinkWrapper( IMqtt3Sink sink ) => _sink = sink;

        public IConnectedMessageSender Sender { get; set; } = null!; //Set by the client.

        public void OnPacketResent( ushort packetId, ulong packetInTransitOrLost, bool isDropped ) => _sink.OnPacketResent( packetId, packetInTransitOrLost, isDropped );

        public void OnPacketWithDupFlagReceived( PacketType packetType ) => _sink.OnPacketWithDupFlagReceived( packetType );

        public void OnQueueFullPacketDropped( ushort packetId ) => _sink.OnQueueFullPacketDropped( packetId );

        public void OnQueueFullPacketDropped( ushort packetId, PacketType packetType ) => _sink.OnQueueFullPacketDropped( packetId, packetType );

        public bool OnUnattendedDisconnect( DisconnectReason reason ) => _sink.OnUnattendedDisconnect( reason );

        public void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData ) => _sink.OnUnparsedExtraData( packetId, unparsedData );

        public ValueTask ReceiveAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken ) => _sink.ReceiveAsync( topic, reader, size, q, retain, cancellationToken );
    }
}
