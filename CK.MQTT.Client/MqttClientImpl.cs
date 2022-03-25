using CK.MQTT.Client;
using CK.MQTT.Common.Pumps;
using CK.MQTT.Packets;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class MqttClientImpl : IMqttClient
    {
        protected class ClientState : IState
        {
            public ClientState( OutputPump outputPump, IMqttChannel channel )
            {
                OutputPump = outputPump;
                Channel = channel;
            }

            public readonly IMqttChannel Channel;
            public OutputPump OutputPump { get; }

            public Task CloseAsync()
            {
                Channel.Close();
                Channel.Dispose();
                return Task.CompletedTask;
            }
        }


        protected DuplexPump<ClientState>? Pumps { get; set; }

        protected Mqtt3ClientConfiguration Config { get; }

        readonly IRemotePacketStore _remotePacketStore;
        readonly ILocalPacketStore _localPacketStore;

        /// <summary>
        /// Instantiate the <see cref="MqttClientImpl"/> with the given configuration.
        /// </summary>
        /// <param name="config">The configuration to use.</param>
        /// <param name="messageHandler">The delegate that will handle incoming messages. <see cref="MessageHandlerDelegate"/> docs for more info.</param>
        public MqttClientImpl( IMqtt3Sink sink, Mqtt3ClientConfiguration config )
        {
            Config = config;
            if( config.WaitTimeoutMilliseconds > config.KeepAliveSeconds * 1000 && config.KeepAliveSeconds != 0 )
            {
                throw new ArgumentException( "Wait timeout should be smaller than the keep alive." );
            }
            _sink = sink;
            _remotePacketStore = config.RemotePacketStore;
            _localPacketStore = config.LocalPacketStore;
        }

        /// <summary>
        /// This method is required so the delegate used in the Reflex doesn't change.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        protected ValueTask OnMessageAsync( string topic, PipeReader pipeReader, uint payloadLength, QualityOfService qos, bool retain, CancellationToken cancellationToken )
            => _sink.ReceiveAsync( topic, pipeReader, payloadLength, qos, retain, cancellationToken );



        /// <inheritdoc/>
        public async Task<ConnectResult> ConnectAsync( OutgoingLastWill? lastWill = null, CancellationToken cancellationToken = default )
        {
            if( lastWill != null )
            {
                MqttBinaryWriter.ThrowIfInvalidMQTTString( lastWill.Topic );
            }

            if( Pumps?.IsRunning ?? false ) throw new InvalidOperationException( "This client is already connected." );

            var res = await DoConnectAsync( lastWill, cancellationToken );
            if( res.ConnectReturnCode == ConnectReturnCode.Accepted && Config.DisconnectBehavior == DisconnectBehavior.AutoReconnect )
            {
                _autoReconnectTask = ReconnectBackgroundAsync();
            }
            return res;
        }



        async Task<ConnectResult> DoConnectAsync( OutgoingLastWill? lastWill, CancellationToken cancellationToken )
        {
            try
            {
                if( Config.DisconnectBehavior == DisconnectBehavior.AutoReconnect )
                {
                    _disconnectTCS = new TaskCompletionSource();
                }
                // Creating channel. The channel is not yet connected, but other object need a reference to it.
                IMqttChannel channel = await Config.ChannelFactory.CreateAsync( Config.ConnectionString );

                // Creating pumps. Need to be started.
                OutputPump output = new( _sink, _localPacketStore, SelfDisconnectAsync, Config );

                // Middleware that will processes the requests.
                ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder()
                    .UseMiddleware( new PublishReflex( Config, _remotePacketStore, OnMessageAsync, output ) )
                    .UseMiddleware( new PublishLifecycleReflex( _remotePacketStore, _localPacketStore, output ) )
                    .UseMiddleware( new SubackReflex( _localPacketStore ) )
                    .UseMiddleware( new UnsubackReflex( _localPacketStore ) );

                await channel.StartAsync(); // Will create the connection to server.

                OutputProcessor outputProcessor;
                // Enable keepalive only if we need it.
                if( Config.KeepAliveSeconds == 0 )
                {
                    outputProcessor = new OutputProcessor( Config.ProtocolConfiguration, output, channel.DuplexPipe.Output, _localPacketStore );
                }
                else
                {
                    // If keepalive is enabled, we add it's handler to the middlewares.
                    OutputProcessorWithKeepAlive withKeepAlive = new( Config, output, channel.DuplexPipe.Output, _localPacketStore, _remotePacketStore ); // Require channel started.
                    outputProcessor = withKeepAlive;
                    builder.UseMiddleware( withKeepAlive );
                }
                // This reflex handle the connection packet.
                // It will replace itself with the regular packet processing.
                ConnectAckReflex connectAckReflex = new
                (
                    builder.Build( async ( _, _, _, _, _, _ ) =>
                    {
                        await SelfDisconnectAsync( DisconnectReason.ProtocolError );
                        return OperationStatus.Done;
                    } )
                );
                // When receiving the ConnAck, this reflex will replace the reflex with this property.

                Pumps = new DuplexPump<ClientState>( // Require channel started.
                    new ClientState( output, channel ),
                    output,
                    new InputPump( _sink, SelfDisconnectAsync, Config, channel.DuplexPipe.Input, connectAckReflex.HandleRequestAsync )
                );
                output.StartPumping( outputProcessor ); // Start processing incoming messages.

                OutgoingConnect outgoingConnect = new( Config, lastWill );

                WriteResult writeConnectResult;
                using( CancellationTokenSource cts = Config.CancellationTokenSourceFactory.Create( Config.WaitTimeoutMilliseconds ) )
                {
                    // Send the packet.
                    writeConnectResult = await outgoingConnect.WriteAsync( Config.ProtocolConfiguration.ProtocolLevel, channel.DuplexPipe.Output, cts.Token );
                }

                async ValueTask<ConnectResult> Exit( ConnectError connectError )
                {
                    await Pumps!.CloseAsync();
                    return new ConnectResult( connectError );
                }

                if( writeConnectResult != WriteResult.Written )
                    return await Exit( ConnectError.Timeout );
                ConnectResult res;
                using( CancellationTokenSource cts2 = Config.CancellationTokenSourceFactory.Create( cancellationToken, Config.WaitTimeoutMilliseconds ) )
                using( cts2.Token.Register( () => connectAckReflex.TrySetCanceled( cancellationToken ) ) )
                {
                    try
                    {
                        res = await connectAckReflex.Task;
                    }
                    catch( OperationCanceledException )
                    {
                        // This following code wouldn't be better with a sort of ... switch/pattern matching ?
                        if( cancellationToken.IsCancellationRequested )
                        {
                            return await Exit( ConnectError.Connection_Cancelled );
                        }
                        else
                        {
                            return await Exit( ConnectError.Timeout );
                        }
                    }
                }

                // This following code wouldn't be better with a sort of ... switch/pattern matching ?
                if( cancellationToken.IsCancellationRequested )
                    return await Exit( ConnectError.Connection_Cancelled );
                if( connectAckReflex.Task.Exception is not null || connectAckReflex.Task.IsFaulted )
                    return await Exit( ConnectError.InternalException );
                if( Pumps.IsClosed )
                    return await Exit( ConnectError.RemoteDisconnected );

                if( res.ConnectError != ConnectError.None )
                {
                    await Pumps!.CloseAsync();
                    return res;
                }

                bool askedCleanSession = Config.Credentials?.CleanSession ?? true;
                if( askedCleanSession && res.SessionState != SessionState.CleanSession )
                    return await Exit( ConnectError.ProtocolError_SessionNotFlushed );
                if( res.SessionState == SessionState.CleanSession )
                {
                    ValueTask task = _remotePacketStore.ResetAsync();
                    await _localPacketStore.ResetAsync();
                    await task;
                }
                else
                {
                    throw new NotImplementedException();
                }
                return res;
            }
            catch( Exception )
            {
                // We may throw before the creation of the duplex pump.
                if( Pumps is not null )
                {
                    await Pumps.CloseAsync();
                    Pumps.Dispose();
                }
                return new ConnectResult( ConnectError.InternalException );
            }
        }

        public bool IsConnected => Pumps?.IsRunning ?? false;

        readonly IMqtt3Sink _sink;

        protected async ValueTask SelfDisconnectAsync( DisconnectReason disconnectedReason )
        {
            Debug.Assert( Pumps != null );
            await Pumps.CloseAsync();
            _sink.OnUnattendedDisconnect( disconnectedReason );
            _disconnectTCS?.TrySetResult(); //TrySet because we can have the user ask concurrently to Disconnect.
        }

        CancellationTokenSource? _running;

        Task? _autoReconnectTask;
        TaskCompletionSource? _disconnectTCS;
        public async Task ReconnectBackgroundAsync()
        {
            await _disconnectTCS!.Task;
            _running = new();
            while( !_running.IsCancellationRequested )
            {
                ConnectResult result = await DoConnectAsync( null, _running.Token );
                if( result.ConnectError != ConnectError.None ) continue;
                await _disconnectTCS.Task;
            }
        }


        public ValueTask<Task<T?>> SendPacketAsync<T>( IOutgoingPacket outgoingPacket )
        {
            if( !IsConnected ) throw new InvalidOperationException( "Client is Disconnected." );
            DuplexPump<ClientState>? duplex = Pumps;
            if( duplex is null ) throw new NullReferenceException();
            return SenderHelper.SendPacketAsync<T>( _localPacketStore, duplex.State.OutputPump, outgoingPacket );
        }

        public async ValueTask<Task> PublishAsync( OutgoingMessage message )
        {
            MqttBinaryWriter.ThrowIfInvalidMQTTString( message.Topic );
            return await SendPacketAsync<object?>( message );
        }

        /// <inheritdoc/>
        public ValueTask<Task<SubscribeReturnCode[]>> SubscribeAsync( IEnumerable<Subscription> subscriptions )
        {
            var subs = subscriptions.ToArray();
            foreach( Subscription sub in subs )
            {
                MqttBinaryWriter.ThrowIfInvalidMQTTString( sub.TopicFilter );
            }
            return SendPacketAsync<SubscribeReturnCode[]>( new OutgoingSubscribe( subs ) )!;
        }

        /// <inheritdoc/>
        public ValueTask<Task<SubscribeReturnCode>> SubscribeAsync( Subscription subscriptions )
        {
            MqttBinaryWriter.ThrowIfInvalidMQTTString( subscriptions.TopicFilter );
            return SendPacketAsync<SubscribeReturnCode>( new OutgoingSubscribe( new[] { subscriptions } ) );
        }

        /// <inheritdoc/>
        public async ValueTask<Task> UnsubscribeAsync( params string[] topics )
        {
            foreach( string topic in topics )
            {
                MqttBinaryWriter.ThrowIfInvalidMQTTString( topic );
            }
            return await SendPacketAsync<object>( new OutgoingUnsubscribe( topics ) );
        }

        /// <summary>
        /// Called by the external world to explicitly close the connection to the remote.
        /// </summary>
        /// <returns>True if this call actually closed the connection, false if the connection has already been closed by a concurrent decision.</returns>
        public async Task<bool> DisconnectAsync( bool clearSession, CancellationToken cancellationToken )
        {
            _running?.Cancel();
            _disconnectTCS?.TrySetResult();
            if( _autoReconnectTask != null ) await _autoReconnectTask;
            DuplexPump<ClientState>? duplex = Pumps;
            if( duplex is null ) return false;
            if( !duplex.IsRunning ) return false;
            await duplex.StopWorkAsync();
            _localPacketStore.CancelAllAckTask(); //Cancel acks when we know no more work will come.
            if( duplex.IsClosed ) return false;
            // Because we stopped the pumps, their states cannot change concurrently.

            //TODO: the return type may be not enough there, if the cancellation token was triggered, we may not know the final
            await OutgoingDisconnect.Instance.WriteAsync( Config.ProtocolConfiguration.ProtocolLevel, duplex.State.Channel.DuplexPipe.Output, cancellationToken );
            await duplex.CloseAsync();
            duplex.Dispose();
            Pumps = null;
            return true;
        }

        public void Dispose()
        {
            Pumps?.Dispose();
        }
    }
}
