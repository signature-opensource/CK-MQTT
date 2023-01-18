using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public class MQTTMessageSink : IMQTT3Sink
    {
        protected readonly ChannelWriter<object?> _events;
        public MQTTMessageSink( ChannelWriter<object?> Events )
        {
            _events = Events;
        }
        public IConnectedMessageSender Sender { get; set; } = null!; //set by the client.

        record PacketResent( ushort PacketId, ulong ResentCount, bool IsDropped );

        public void OnPacketResent( ushort packetId, ulong resentCount, bool isDropped )
            => _events.TryWrite( new PacketResent( packetId, resentCount, isDropped ) );

        public record UnattendedDisconnect( DisconnectReason Reason );
        public void OnUnattendedDisconnect( DisconnectReason reason )
        {
            _events.TryWrite( new UnattendedDisconnect( reason ) );
        }

        public record UserDisconnect( bool clearSession );
        public void OnUserDisconnect( bool clearSession )
            => _events.TryWrite( new UserDisconnect( clearSession ) );

        public record UnparsedExtraData( ushort PacketId, ReadOnlySequence<byte> UnparsedData );
        public void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData )
            => _events.TryWrite( new UnparsedExtraData( packetId, unparsedData ) );

        public async ValueTask OnMessageAsync( string topic, PipeReader pipe, uint payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken )
        {
            var memoryOwner = MemoryPool<byte>.Shared.Rent( (int)payloadLength );
            if( payloadLength != 0 )
            {
                ReadResult readResult = await pipe.ReadAtLeastAsync( (int)payloadLength, cancellationToken );
                readResult.Buffer.Slice( 0, (int)payloadLength ).CopyTo( memoryOwner.Memory.Span );
                pipe.AdvanceTo( readResult.Buffer.Slice( Math.Min( payloadLength, readResult.Buffer.Length ) ).Start );
            }
            await _events.WriteAsync( new VolatileApplicationMessage( new ApplicationMessage( topic, memoryOwner.Memory.Slice( 0, (int)payloadLength ), qos, retain ), memoryOwner ), cancellationToken );
        }

        public record QueueFullPacketDestroyed( ushort PacketId, PacketType PacketType );
        public void OnQueueFullPacketDropped( ushort packetId, PacketType packetType )
            => _events.TryWrite( new QueueFullPacketDestroyed( packetId, packetType ) );

        public record QueueFullPacketDestroyed2( ushort PacketId );
        public void OnQueueFullPacketDropped( ushort packetId )
            => _events.TryWrite( new QueueFullPacketDestroyed2( packetId ) );

        public record PacketWithDupFlagReceived( PacketType packetType );
        public void OnPacketWithDupFlagReceived( PacketType packetType )
            => _events.TryWrite( new PacketWithDupFlagReceived( packetType ) );


    }
}
