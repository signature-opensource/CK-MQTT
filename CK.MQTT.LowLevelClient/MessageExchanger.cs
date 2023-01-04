using CK.MQTT.Client;
using CK.MQTT.Common.Pumps;
using CK.MQTT.Packets;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public abstract class MessageExchanger : IConnectedLowLevelMQTTClient
    {
        /// <summary>
        /// Instantiate the <see cref="MessageExchanger"/> with the given configuration.
        /// </summary>
        /// <param name="config">The configuration to use.</param>
        /// <param name="messageHandler">The delegate that will handle incoming messages. <see cref="MessageHandlerDelegate"/> docs for more info.</param>
        public MessageExchanger( ProtocolConfiguration pConfig, MQTT3ConfigurationBase config, IMQTT3Sink sink, IMQTTChannel channel, IRemotePacketStore? remotePacketStore = null, ILocalPacketStore? localPacketStore = null )
        {
            PConfig = pConfig;
            Config = config;
            Sink = sink;
            sink.Sender = this;
            Channel = channel;
            RemotePacketStore = remotePacketStore ?? new MemoryPacketIdStore();
            LocalPacketStore = localPacketStore ?? new MemoryPacketStore( pConfig, Config, ushort.MaxValue );
        }

        /// <inheritdoc/>
        public abstract string? ClientId { get; }
        public MQTT3ConfigurationBase Config { get; }
        public virtual IMQTT3Sink Sink { get; }
        public IRemotePacketStore RemotePacketStore { get; }
        public ILocalPacketStore LocalPacketStore { get; }
        public IMQTTChannel Channel { get; }
        public ProtocolConfiguration PConfig { get; }
        public DuplexPump<OutputPump, InputPump>? Pumps { get; protected set; }
        public bool IsConnected => Pumps?.IsRunning ?? false;

        protected ValueTask<Task<T?>> SendPacketWithQoSAsync<T>( IOutgoingPacket outgoingPacket )
            => outgoingPacket.Qos switch
            {
                QualityOfService.AtLeastOnce => StoreAndSendAsync<T>( outgoingPacket ),
                QualityOfService.ExactlyOnce => StoreAndSendAsync<T>( outgoingPacket ),
                _ => throw new ArgumentException( "Invalid QoS." ),
            };

        [ThreadColor( ThreadColor.Rainbow )]
        async ValueTask<Task<T?>> StoreAndSendAsync<T>( IOutgoingPacket msg )
        {
            (Task<object?> ackReceived, IOutgoingPacket newPacket) = await LocalPacketStore.StoreMessageAsync( msg, msg.Qos );
            return SendAsync<T>( newPacket, ackReceived );
        }

        [ThreadColor( ThreadColor.Rainbow )]
        async Task<T?> SendAsync<T>( IOutgoingPacket packet, Task<object?> ackReceived )
        {
            QueueMessageIfConnected( packet );
            object? res = await ackReceived;
            if( res is null ) return default;
            if( res is T a ) return a;

            // For example: it will throw if the client send a Publish, and the server answer a SubscribeAck with the same packet id as the publish.
            throw new ProtocolViolationException( $"Expected to find a {typeof( T )} in the store for packet ID {packet.PacketId}, but got {res.GetType()}. This is an implementation bug from the server, or client, or the network didn't respected it's guarantees." );
        }

        public ValueTask<Task> PublishAsync( OutgoingMessage message )
        {
            if( message.Qos == QualityOfService.AtMostOnce ) return PublishQoS0Async( message );
            var vtask = SendPacketWithQoSAsync<object?>( message );
            return UnwrapCastAsync( message, vtask );
        }

        static async ValueTask<Task> UnwrapCastAsync<T>( OutgoingMessage message, ValueTask<Task<T>> vtask )
        {
            var task = await vtask;
            await message.DisposeAsync();
            return task;
        }

        async ValueTask<Task> PublishQoS0Async( OutgoingMessage packet )
        {
            var pumps = Pumps;
            if( pumps != null )
            {
                await pumps.Left.QueueMessageAsync( packet );
            }
            else
            {
                await packet.DisposeAsync();
            }
            return Task.CompletedTask;
        }

        [ThreadColor( ThreadColor.Rainbow )]
        void QueueMessageIfConnected( IOutgoingPacket packet )
        {
            var pumps = Pumps;
            pumps?.Left.TryQueueMessage( packet );
        }
        /// <returns><see langword="true"/> if the sink asked to reconnect.</returns>
        internal protected async virtual ValueTask<bool> SelfDisconnectAsync( DisconnectReason disconnectedReason )
        {
            Debug.Assert( Pumps != null );
            await Channel.CloseAsync( disconnectedReason );
            await Pumps.StopWorkAsync();
            return Sink.OnUnattendedDisconnect( disconnectedReason );
        }

        /// <summary>
        /// Called by the external world to explicitly close the connection to the remote.
        /// </summary>
        /// <returns>True if this call actually closed the connection, false if the connection has already been closed by a concurrent decision.</returns>
        public async Task<bool> DisconnectAsync( bool clearSession )
        {
            var pumps = Pumps;
            if( pumps is null ) return false;
            if( !pumps.IsRunning ) return false;
            await pumps.StopWorkAsync();
            LocalPacketStore.CancelAllAckTask(); //Cancel acks when we know no more work will come.
            if( pumps.IsClosed ) return false;
            // Because we stopped the pumps, their states won't change anymore.
            var channel = Channel;
            if( !(channel?.IsConnected ?? false) ) return false;
            var duplexPipe = channel.DuplexPipe;
            if( duplexPipe == null ) return false;
            await BeforeUserDisconnectAsync( duplexPipe, clearSession );
            await LocalPacketStore.ResetAsync();
            await RemotePacketStore.ResetAsync();

            await pumps.DisposeAsync();
            Pumps = null;
            return true;
        }

        protected virtual ValueTask BeforeUserDisconnectAsync( IDuplexPipe duplexPipe, bool clearSession ) => new();

        [ThreadColor( ThreadColor.None )]
        public virtual async ValueTask DisposeAsync()
        {
            var pumps = Pumps;
            if( pumps is not null )
            {
                await pumps.DisposeAsync();
            }
        }
    }
}




