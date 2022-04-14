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
            Start();
        }

        protected Channel<object?>? Messages { get; set; }

        [MemberNotNull( nameof( Messages ) )]
        public virtual void Start()
        {
            Messages ??= Channel.CreateUnbounded<object?>( new UnboundedChannelOptions() { SingleReader = true, SingleWriter = false } );
        }

        record PacketResent( ushort PacketId, int ResentCount, bool IsDropped );
        record PoisonousPacket( ushort PacketId, PacketType PacketType, int PoisnousTotalCount );

        protected override void OnPacketResent( ushort packetId, int resentCount, bool isDropped )
            => Messages!.Writer.TryWrite( new PacketResent( packetId, resentCount, isDropped ) );

        protected override void OnPoisonousPacket( ushort packetId, PacketType packetType, int poisonousTotalCount )
            => Messages!.Writer.TryWrite( new PoisonousPacket( packetId, packetType, poisonousTotalCount ) );

        protected class Connected { };
        protected override void OnConnected() => Messages!.Writer.TryWrite( new Connected() );

        protected record StoreFilling( ushort FreeLeftSlot );
        protected override void OnStoreFull( ushort freeLeftSlot )
            => Messages!.Writer.TryWrite( new StoreFilling( freeLeftSlot ) );

        protected record UnattendedDisconnect( DisconnectReason Reason );
        protected override void OnUnattendedDisconnect( DisconnectReason reason )
            => Messages!.Writer.TryWrite( new UnattendedDisconnect( reason ) );

        protected record UnparsedExtraData( ushort PacketId, ReadOnlySequence<byte> UnparsedData );
        protected override void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData )
            => Messages!.Writer.TryWrite( new UnparsedExtraData( packetId, unparsedData ) );

        protected override async ValueTask ReceiveAsync( string topic, PipeReader pipe, uint payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken )
        {
            Memory<byte> buffer = new byte[payloadLength];
            if( payloadLength != 0 )
            {
                ReadResult readResult = await pipe.ReadAtLeastAsync( (int)payloadLength, cancellationToken );
                readResult.Buffer.Slice( 0, (int)payloadLength ).CopyTo( buffer.Span );
                pipe.AdvanceTo( readResult.Buffer.Slice( Math.Min( payloadLength, readResult.Buffer.Length ) ).Start );
            }
            await Messages!.Writer.WriteAsync( new ApplicationMessage( topic, buffer, qos, retain ), cancellationToken ); //Todo: DisposableApplicationMessage
        }

        protected record ReconnectionFailed( int RetryCount, int MaxRetryCount );
        protected override bool OnReconnectionFailed( int retryCount, int maxRetryCount )
        {
            Messages!.Writer.TryWrite( new ReconnectionFailed( retryCount, maxRetryCount ) );
            return true;
        }

        protected record QueueFullPacketDestroyed( ushort PacketId, PacketType PacketType );
        protected override void OnQueueFullPacketDropped( ushort packetId, PacketType packetType )
            => Messages!.Writer.TryWrite( new QueueFullPacketDestroyed( packetId, packetType ) );
    }
}
