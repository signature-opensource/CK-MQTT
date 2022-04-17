using CK.MQTT.Client;
using CK.MQTT.Common.Pumps;
using CK.MQTT.Packets;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class MessageExchanger : IConnectedLowLevelMqttClient
    {
        /// <summary>
        /// Instantiate the <see cref="MessageExchanger"/> with the given configuration.
        /// </summary>
        /// <param name="config">The configuration to use.</param>
        /// <param name="messageHandler">The delegate that will handle incoming messages. <see cref="MessageHandlerDelegate"/> docs for more info.</param>
        public MessageExchanger( ProtocolConfiguration pConfig, Mqtt3ConfigurationBase config, IMqtt3Sink sink, IMqttChannel channel, IRemotePacketStore? remotePacketStore = null, ILocalPacketStore? localPacketStore = null )
        {
            PConfig = pConfig;
            Config = config;
            Sink = sink;
            Channel = channel;
            RemotePacketStore = remotePacketStore ?? new MemoryPacketIdStore();
            LocalPacketStore = localPacketStore ?? new MemoryPacketStore( pConfig, Config, ushort.MaxValue );
        }

        public Mqtt3ConfigurationBase Config { get; }
        internal protected virtual IMqtt3Sink Sink { get; protected set; }
        internal protected IRemotePacketStore RemotePacketStore { get; }
        internal protected ILocalPacketStore LocalPacketStore { get; }
        internal protected IMqttChannel Channel { get; }
        internal protected ProtocolConfiguration PConfig { get; }
        internal protected DuplexPump<OutputPump, InputPump>? Pumps { get; protected set; }
        public bool IsConnected => Pumps?.IsRunning ?? false;

        protected async ValueTask<Task<T?>> SendPacketAsync<T>( IOutgoingPacket outgoingPacket )
        {
            if( !IsConnected ) throw new InvalidOperationException( "Client is Disconnected." );
            var duplex = Pumps;
            if( duplex is null ) throw new NullReferenceException();
            return outgoingPacket.Qos switch
            {
                QualityOfService.AtMostOnce => await PublishQoS0Async<T>( outgoingPacket ),
                QualityOfService.AtLeastOnce => await StoreAndSendAsync<T>( outgoingPacket ),
                QualityOfService.ExactlyOnce => await StoreAndSendAsync<T>( outgoingPacket ),
                _ => throw new ArgumentException( "Invalid QoS." ),
            };
        }

        async ValueTask<Task<T?>> PublishQoS0Async<T>( IOutgoingPacket packet )
        {
            await QueueMessageIfConnectedAsync( packet );
            return Task.FromResult<T?>( default );
        }

        [ThreadColor( ThreadColor.Rainbow )]
        async ValueTask QueueMessageIfConnectedAsync( IOutgoingPacket packet )
        {
            var pumps = Pumps;
            if( pumps != null )
            {
                await pumps.Left.QueueMessageAndWaitUntilSentAsync( packet );
            }
        }

        [ThreadColor( ThreadColor.Rainbow )]
        async ValueTask<Task<T?>> StoreAndSendAsync<T>( IOutgoingPacket msg )
        {
            (Task<object?> ackReceived, IOutgoingPacket newPacket) = await LocalPacketStore.StoreMessageAsync( msg, msg.Qos );
            return SendAsync<T>( newPacket, ackReceived );
        }

        [ThreadColor( ThreadColor.Rainbow )]
        async Task<T?> SendAsync<T>( IOutgoingPacket packet, Task<object?> ackReceived )
        {
            await QueueMessageIfConnectedAsync( packet );
            object? res = await ackReceived;
            if( res is null ) return default;
            if( res is T a ) return a;
            //For example: it will throw if the client send a Publish, and the server answer a SubscribeAck with the same packet id as the publish.
            throw new ProtocolViolationException( "We received a packet id ack of an unexpected packet type." );
        }

        public async ValueTask<Task> PublishAsync( OutgoingMessage message ) => await SendPacketAsync<object?>( message );

        internal protected async virtual ValueTask SelfDisconnectAsync( DisconnectReason disconnectedReason )
        {
            Debug.Assert( Pumps != null );
            Channel.Close();
            await Pumps.StopWorkAsync();
            Sink.OnUnattendedDisconnect( disconnectedReason );
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
            await BeforeFullDisconnectAsync( duplexPipe, clearSession );
            await LocalPacketStore.ResetAsync();
            await RemotePacketStore.ResetAsync();

            pumps.Dispose();
            Pumps = null;
            return true;
        }

        protected virtual ValueTask BeforeFullDisconnectAsync( IDuplexPipe duplexPipe, bool clearSession )
        {
            return new ValueTask();
        }

        public void Dispose()
        {
            Pumps?.Dispose();
        }
    }
}




