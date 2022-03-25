using CK.MQTT.Packets;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public abstract class MQTT3ClientBase : IMqtt3Sink, IMqtt3Client
    {
        readonly IMqtt3Client _client;

        protected MQTT3ClientBase( Mqtt3ClientConfiguration configuration )
        {
            _client = new MqttClientImpl( this, configuration );
        }

        public bool IsConnected => _client.IsConnected;

        protected MQTT3ClientBase( string host, int port ) : this( new Mqtt3ClientConfiguration( $"{host}:{port}" ) )
        {
        }

        protected abstract ValueTask ReceiveAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken );

        public virtual Task<ConnectResult> ConnectAsync( OutgoingLastWill? lastwill = null, CancellationToken cancellationToken = default )
        {
            return _client.ConnectAsync( lastwill, cancellationToken );
        }

        public virtual Task<bool> DisconnectAsync( bool deleteSession, CancellationToken cancellationToken )
        {
            return _client.DisconnectAsync( deleteSession, cancellationToken );
        }

        public virtual ValueTask<Task> UnsubscribeAsync( IEnumerable<string> topics )
        {
            return _client.UnsubscribeAsync( topics.ToArray() );
        }


        protected abstract void OnUnattendedDisconnect( DisconnectReason reason );

        protected virtual bool OnReconnectionFailed( int retryCount, int maxRetryCount ) => retryCount < maxRetryCount;

        protected abstract void OnReconnect();

        protected abstract void OnStoreFull( ushort freeLeftSlot );

        protected abstract void OnPoisonousPacket( ushort packetId, PacketType packetType, int poisonousTotalCount );

        protected abstract void OnPacketResent( ushort packetId, int resentCount, bool isDropped );

        protected virtual void OnQueueFullPacketDropped( ushort packetId, PacketType packetType )
        {
        }
        protected abstract void OnUnparsedExtraData( ushort packetId, System.Buffers.ReadOnlySequence<byte> unparsedData );

        public virtual ValueTask<Task<SubscribeReturnCode>> SubscribeAsync( Subscription subscription )
            => _client.SubscribeAsync( subscription );

        public virtual ValueTask<Task<SubscribeReturnCode[]>> SubscribeAsync( IEnumerable<Subscription> subscriptions )
            => _client.SubscribeAsync( subscriptions );

        // Helper (extension methods).
        public ValueTask<Task<SubscribeReturnCode[]>> SubscribeAsync( params Subscription[] subscriptions )
            => SubscribeAsync( (IEnumerable<Subscription>)subscriptions );

        //protected IPacketStore StoreInspector { get; }



        // Helper (extension methods).
        public ValueTask<Task> UnsubscribeAsync( params string[] topics ) => UnsubscribeAsync( (IEnumerable<string>)topics );

        // Helper (extension methods).
        public ValueTask<Task> UnsubscribeAsync( string topic )
        {
            return _client.UnsubscribeAsync( topic );
        }

        public virtual ValueTask<Task> PublishAsync( OutgoingMessage message )
        {
            return _client.PublishAsync( message );
        }

        // Helper (extension methods).
        public ValueTask<Task> PublishAsync( string topic, QualityOfService qos, bool retain, ReadOnlyMemory<byte> payload )
        {
            return _client.PublishAsync( new SmallOutgoingApplicationMessage( topic, qos, retain, payload ) );
        }

        ValueTask IMqtt3Sink.ReceiveAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken )
        {
            return ReceiveAsync( topic, reader, size, q, retain, cancellationToken );
        }

        void IMqtt3Sink.OnUnattendedDisconnect( DisconnectReason reason ) => OnUnattendedDisconnect( reason );

        bool IMqtt3Sink.OnReconnectionFailed( int retryCount, int maxRetryCount ) => OnReconnectionFailed( retryCount, maxRetryCount );

        void IMqtt3Sink.OnReconnect() => OnReconnect();

        void IMqtt3Sink.OnStoreFull( ushort freeLeftSlot ) => OnStoreFull( freeLeftSlot );

        void IMqtt3Sink.OnPoisonousPacket( ushort packetId, PacketType packetType, int poisonousTotalCount ) => OnPoisonousPacket( packetId, packetType, poisonousTotalCount );

        void IMqtt3Sink.OnPacketResent( ushort packetId, int resentCount, bool isDropped ) => OnPacketResent( packetId, resentCount, isDropped );

        void IMqtt3Sink.OnQueueFullPacketDropped( ushort packetId, PacketType packetType ) => OnQueueFullPacketDropped( packetId, packetType );
        void IMqtt3Sink.OnUnparsedExtraData( ushort packetId, System.Buffers.ReadOnlySequence<byte> unparsedData )
            => OnUnparsedExtraData( packetId, unparsedData );

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
