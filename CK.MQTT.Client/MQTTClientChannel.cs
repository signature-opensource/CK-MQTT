using CK.Core;
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
    class MQTTClientChannel : Mqtt3ClientBase
    {
        readonly Channel<object?> _channel = Channel.CreateUnbounded<object?>( new UnboundedChannelOptions()
        {
            SingleReader = true,
            SingleWriter = false
        } );

        public ChannelReader<object?> Messages => _channel;

        public MQTTClientChannel( Mqtt3ClientConfiguration configuration ) : base( configuration )
        {
        }

        record PacketResent( ushort PacketId, int ResentCount, bool IsDropped );
        record PoisonousPacket( ushort PacketId, PacketType PacketType, int PoisnousTotalCount );

        protected override void OnPacketResent( ushort packetId, int resentCount, bool isDropped )
            => _channel.Writer.TryWrite( new PacketResent( packetId, resentCount, isDropped ) );

        protected override void OnPoisonousPacket( ushort packetId, PacketType packetType, int poisonousTotalCount )
        {
            _channel.Writer.TryWrite( new PoisonousPacket( packetId, packetType, poisonousTotalCount ) );
        }

        internal class Connected { };
        protected override void OnConnected() => _channel.Writer.TryWrite( new Connected() );

        internal record StoreFilling( ushort FreeLeftSlot );
        protected override void OnStoreFull( ushort freeLeftSlot )
        {
            _channel.Writer.TryWrite( new StoreFilling( freeLeftSlot ) );
        }

        internal record UnattendedDisconnect( DisconnectReason Reason );
        protected override void OnUnattendedDisconnect( DisconnectReason reason )
        {
            _channel.Writer.TryWrite( new UnattendedDisconnect( reason ) );
        }

        internal record UnparsedExtraData( ushort PacketId, ReadOnlySequence<byte> UnparsedData );
        protected override void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData )
        {
            _channel.Writer.TryWrite( new UnparsedExtraData( packetId, unparsedData ) );
        }

        protected override async ValueTask ReceiveAsync( string topic, PipeReader pipe, uint payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken )
        {
            Memory<byte> buffer = new byte[payloadLength];
            if( payloadLength != 0 )
            {
                ReadResult readResult = await pipe.ReadAtLeastAsync( (int)payloadLength, cancellationToken );
                readResult.Buffer.Slice( 0, (int)payloadLength ).CopyTo( buffer.Span );
                pipe.AdvanceTo( readResult.Buffer.Slice( Math.Min( payloadLength, readResult.Buffer.Length ) ).Start );
            }
            _channel.Writer.TryWrite( new ApplicationMessage( topic, buffer, qos, retain ) ); //Todo: DisposableApplicationMessage
        }

        internal record ReconnectionFailed( int RetryCount, int MaxRetryCount );
        protected override bool OnReconnectionFailed( int retryCount, int maxRetryCount )
        {
            _channel.Writer.TryWrite( new ReconnectionFailed( retryCount, maxRetryCount ) );
            return true;
        }

        internal record QueueFullPacketDestroyed( ushort PacketId, PacketType PacketType );
        protected override void OnQueueFullPacketDropped( ushort packetId, PacketType packetType )
        {
            _channel.Writer.TryWrite( new QueueFullPacketDestroyed( packetId, packetType ) );
        }
    }
}
