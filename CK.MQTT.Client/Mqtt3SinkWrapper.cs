using CK.MQTT.Packets;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public abstract class Mqtt3SinkWrapper<T> : IMqtt3Sink, IConnectedMessageExchanger where T : IConnectedMessageExchanger
    {
        public T Client { get; }

        protected Mqtt3SinkWrapper( Func<IMqtt3Sink, T> clientFactory )
        {
            Client = clientFactory( this );
        }

        protected abstract ValueTask ReceiveAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken );

        public virtual Task<bool> DisconnectAsync( bool deleteSession ) => Client.DisconnectAsync( deleteSession );

        protected abstract IMqtt3Sink.ManualConnectRetryBehavior OnFailedManualConnect( ConnectResult connectResult );
        protected abstract bool OnUnattendedDisconnect( DisconnectReason reason );
        protected abstract ValueTask<bool> OnReconnectionFailedAsync( ConnectResult result );

        protected abstract void OnConnected();

        protected abstract void OnStoreFull( ushort freeLeftSlot );

        protected abstract void OnPoisonousPacket( ushort packetId, PacketType packetType, int poisonousTotalCount );

        protected abstract void OnPacketResent( ushort packetId, int resentCount, bool isDropped );

        protected virtual void OnQueueFullPacketDropped( ushort packetId, PacketType packetType ) { }
        protected virtual void OnQueueFullPacketDropped( ushort packetId ) { }
        protected abstract void OnUnparsedExtraData( ushort packetId, System.Buffers.ReadOnlySequence<byte> unparsedData );

        protected virtual void OnPacketWithDupFlagReceived( PacketType packetType ) { }

        public ValueTask<Task> PublishAsync( OutgoingMessage message ) => Client.PublishAsync( message );

        public ValueTask<Task> PublishAsync( string topic, QualityOfService qos, bool retain, ReadOnlyMemory<byte> payload )
            => Client.PublishAsync( new SmallOutgoingApplicationMessage( topic, qos, retain, payload ) );

        ValueTask IMqtt3Sink.ReceiveAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken )
            => ReceiveAsync( topic, reader, size, q, retain, cancellationToken );

        bool IMqtt3Sink.OnUnattendedDisconnect( DisconnectReason reason ) => OnUnattendedDisconnect( reason );

        ValueTask<bool> IMqtt3Sink.OnReconnectionFailedAsync( ConnectResult result ) => OnReconnectionFailedAsync(result);

        void IMqtt3Sink.Connected() => OnConnected();

        void IMqtt3Sink.OnPoisonousPacket( ushort packetId, PacketType packetType, int poisonousTotalCount ) => OnPoisonousPacket( packetId, packetType, poisonousTotalCount );

        void IMqtt3Sink.OnPacketResent( ushort packetId, int resentCount, bool isDropped ) => OnPacketResent( packetId, resentCount, isDropped );

        void IMqtt3Sink.OnQueueFullPacketDropped( ushort packetId, PacketType packetType ) => OnQueueFullPacketDropped( packetId, packetType );
        void IMqtt3Sink.OnQueueFullPacketDropped( ushort packetId ) => OnQueueFullPacketDropped( packetId );
        void IMqtt3Sink.OnUnparsedExtraData( ushort packetId, System.Buffers.ReadOnlySequence<byte> unparsedData )
            => OnUnparsedExtraData( packetId, unparsedData );
        void IMqtt3Sink.OnPacketWithDupFlagReceived( PacketType packetType )
            => OnPacketWithDupFlagReceived( packetType );

        IMqtt3Sink.ManualConnectRetryBehavior IMqtt3Sink.OnFailedManualConnect( ConnectResult connectResult ) => OnFailedManualConnect( connectResult );


        public ValueTask DisposeAsync() => Client.DisposeAsync();
    }
}
