using CK.Core;
using CK.MQTT.Packets;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public abstract class MessageExchangerAgentBase<T> : Mqtt3SinkWrapper<T> where T : IConnectedMessageExchanger
    {
        protected MessageExchangerAgentBase( Func<IMqtt3Sink, T> clientFactory ) : base( clientFactory )
        {
        }

        protected Channel<object?>? Events { get; private set; }

        [MemberNotNull( nameof( Events ) )]
        public virtual void Start()
        {
            if( Events != null ) throw new InvalidOperationException( "Already started." );
            Events = Channel.CreateUnbounded<object?>( new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = false
            } );
        }

        protected virtual ValueTask StopAsync( bool waitForCompletion )
        {
            Events!.Writer.Complete();
            Events = null;
            return new ValueTask();
        }

        record PacketResent( ushort PacketId, int ResentCount, bool IsDropped );
        record PoisonousPacket( ushort PacketId, PacketType PacketType, int PoisnousTotalCount );

        protected override void OnPacketResent( ushort packetId, int resentCount, bool isDropped )
            => Events!.Writer.TryWrite( new PacketResent( packetId, resentCount, isDropped ) );

        protected override void OnPoisonousPacket( ushort packetId, PacketType packetType, int poisonousTotalCount )
            => Events!.Writer.TryWrite( new PoisonousPacket( packetId, packetType, poisonousTotalCount ) );

        public record Connected;
        protected override void OnConnected() => Events!.Writer.TryWrite( new Connected() );

        public record StoreFilling( ushort FreeLeftSlot );
        protected override void OnStoreFull( ushort freeLeftSlot )
            => Events!.Writer.TryWrite( new StoreFilling( freeLeftSlot ) );

        public record UnattendedDisconnect( DisconnectReason Reason );
        protected override void OnUnattendedDisconnect( DisconnectReason reason )
            => Events!.Writer.TryWrite( new UnattendedDisconnect( reason ) );

        public record UnparsedExtraData( ushort PacketId, ReadOnlySequence<byte> UnparsedData );
        protected override void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData )
            => Events!.Writer.TryWrite( new UnparsedExtraData( packetId, unparsedData ) );

        protected override async ValueTask ReceiveAsync( string topic, PipeReader pipe, uint payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken )
        {
            Memory<byte> buffer = new byte[payloadLength];
            if( payloadLength != 0 )
            {
                ReadResult readResult = await pipe.ReadAtLeastAsync( (int)payloadLength, cancellationToken );
                readResult.Buffer.Slice( 0, (int)payloadLength ).CopyTo( buffer.Span );
                pipe.AdvanceTo( readResult.Buffer.Slice( Math.Min( payloadLength, readResult.Buffer.Length ) ).Start );
            }
            await Events!.Writer.WriteAsync( new ApplicationMessage( topic, buffer, qos, retain ), cancellationToken ); //Todo: DisposableApplicationMessage
        }

        public record ReconnectionFailed( int RetryCount, int MaxRetryCount );
        protected override bool OnReconnectionFailed( int retryCount, int maxRetryCount )
        {
            Events!.Writer.TryWrite( new ReconnectionFailed( retryCount, maxRetryCount ) );
            return true;
        }

        public record QueueFullPacketDestroyed( ushort PacketId, PacketType PacketType );
        protected override void OnQueueFullPacketDropped( ushort packetId, PacketType packetType )
            => Events!.Writer.TryWrite( new QueueFullPacketDestroyed( packetId, packetType ) );
    }
}
