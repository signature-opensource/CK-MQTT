using CK.Core;
using CK.MQTT.Client.Tests.Helpers;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    class TestMqttClient : Mqtt3ClientBase
    {
        readonly ChannelWriter<object?> _writer;

        public Mqtt3ClientConfiguration Config { get; }

        public TestMqttClient( Mqtt3ClientConfiguration config, ChannelWriter<object?> writer ) : base(config)
        {
            Config = config;
            _writer = writer;
        }

        public record PacketResent( ushort PacketId, int ResentCount, bool IsDropped );
        public record PoisonousPacket( ushort PacketId, PacketType PacketType, int PoisnousTotalCount );

        protected override void OnPacketResent( ushort packetId, int resentCount, bool isDropped )
            => _writer.TryWrite( new PacketResent( packetId, resentCount, isDropped ) );

        protected override void OnPoisonousPacket( ushort packetId, PacketType packetType, int poisonousTotalCount )
        {
            _writer.TryWrite( new PoisonousPacket( packetId, packetType, poisonousTotalCount ) );
        }

        public class Reconnect { };
        protected override void OnConnected()
        {
            _writer.TryWrite( new Reconnect() );
        }

        public record StoreFull( ushort FreeLeftSlot );
        protected override void OnStoreFull( ushort freeLeftSlot )
        {
            _writer.TryWrite( new StoreFull( freeLeftSlot ) );
        }

        public record UnattendedDisconnect( DisconnectReason Reason );
        protected override void OnUnattendedDisconnect( DisconnectReason reason )
        {
            _writer.TryWrite( new UnattendedDisconnect( reason ) );
        }

        public record UnparsedExtraData( ushort PacketId, ReadOnlySequence<byte> UnparsedData );
        protected override void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData )
        {
            _writer.TryWrite( new UnparsedExtraData( packetId, unparsedData ) );
        }

        protected override async ValueTask ReceiveAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken )
        {
            await new NewApplicationMessageClosure( ReceivedMessageAsync ).HandleMessageAsync( topic, reader, size, q, retain, cancellationToken );
        }

        ValueTask ReceivedMessageAsync( IActivityMonitor? m, ApplicationMessage message, CancellationToken cancellationToken )
        {
            _writer.TryWrite( message );
            return new ValueTask();
        }

        public record ReconnectionFailed( int RetryCount, int MaxRetryCount );
        protected override bool OnReconnectionFailed( int retryCount, int maxRetryCount )
        {
            _writer.TryWrite( new ReconnectionFailed( retryCount, maxRetryCount ) );
            return true;
        }

        public record QueueFullPacketDestroyed( ushort PacketId, PacketType PacketType );
        protected override void OnQueueFullPacketDropped( ushort packetId, PacketType packetType )
        {
            _writer.TryWrite( new QueueFullPacketDestroyed( packetId, packetType ) );
        }
    }
}
