using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public class MQTTMessageSink : IMQTT3Sink
    {
        public ChannelWriter<object?> Events { get; set; } = null!;
        public IConnectedMessageSender Sender { get; set; } = null!; //set by the client.

        record PacketResent( ushort PacketId, ulong ResentCount, bool IsDropped );
        record PoisonousPacket( ushort PacketId, PacketType PacketType, int PoisnousTotalCount );

        public void OnPacketResent( ushort packetId, ulong resentCount, bool isDropped )
            => Events.TryWrite( new PacketResent( packetId, resentCount, isDropped ) );

        public void OnPoisonousPacket( ushort packetId, PacketType packetType, int poisonousTotalCount )
            => Events.TryWrite( new PoisonousPacket( packetId, packetType, poisonousTotalCount ) );

        public record StoreFilling( ushort FreeLeftSlot );
        public void OnStoreFull( ushort freeLeftSlot )
            => Events.TryWrite( new StoreFilling( freeLeftSlot ) );

        public record UnattendedDisconnect( DisconnectReason Reason );
        public bool OnUnattendedDisconnect( DisconnectReason reason )
        {
            Events.TryWrite( new UnattendedDisconnect( reason ) );
            return true;
        }

        public record UnparsedExtraData( ushort PacketId, ReadOnlySequence<byte> UnparsedData );
        public void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData )
            => Events.TryWrite( new UnparsedExtraData( packetId, unparsedData ) );

        public async ValueTask OnMessageAsync( string topic, PipeReader pipe, uint payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken )
        {
            var memoryOwner = MemoryPool<byte>.Shared.Rent( (int)payloadLength );
            if( payloadLength != 0 )
            {
                ReadResult readResult = await pipe.ReadAtLeastAsync( (int)payloadLength, cancellationToken );
                readResult.Buffer.Slice( 0, (int)payloadLength ).CopyTo( memoryOwner.Memory.Span );
                pipe.AdvanceTo( readResult.Buffer.Slice( Math.Min( payloadLength, readResult.Buffer.Length ) ).Start );
            }
            await Events.WriteAsync( new VolatileApplicationMessage( new ApplicationMessage( topic, memoryOwner.Memory.Slice( 0, (int)payloadLength ), qos, retain ), memoryOwner ), cancellationToken );
        }

        public record QueueFullPacketDestroyed( ushort PacketId, PacketType PacketType );
        public void OnQueueFullPacketDropped( ushort packetId, PacketType packetType )
            => Events.TryWrite( new QueueFullPacketDestroyed( packetId, packetType ) );

        public record QueueFullPacketDestroyed2( ushort PacketId );
        public void OnQueueFullPacketDropped( ushort packetId )
            => Events.TryWrite( new QueueFullPacketDestroyed2( packetId ) );

        public record PacketWithDupFlagReceived(PacketType packetType);
        public void OnPacketWithDupFlagReceived( PacketType packetType )
            => Events.TryWrite( new PacketWithDupFlagReceived( packetType ) );
    }
}
