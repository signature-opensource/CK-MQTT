using CK.MQTT.Client;
using CK.MQTT.Packets;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public sealed class LowLevelMqttClient : MessageExchanger, IMqtt3Client
    {
        public Mqtt3ClientConfiguration ClientConfig { get; }

        public LowLevelMqttClient(
            ProtocolConfiguration pConfig,
            Mqtt3ClientConfiguration config,
            IMqtt3Sink sink,
            IMqttChannel channel,
            IRemotePacketStore? remotePacketStore = null,
            ILocalPacketStore? localPacketStore = null
        ) : base( pConfig, config, sink, channel, remotePacketStore, localPacketStore )
        {
            if( config.WaitTimeoutMilliseconds > config.KeepAliveSeconds * 1000 && config.KeepAliveSeconds != 0 )
            {
                throw new ArgumentException( "Wait timeout should be smaller than the keep alive." );
            }
            ClientConfig = config;
        }


        /// <inheritdoc/>
        public async Task<ConnectResult> ConnectAsync( OutgoingLastWill? lastWill = null, CancellationToken cancellationToken = default )
        {
            if( lastWill != null )
            {
                MqttBinaryWriter.ThrowIfInvalidMQTTString( lastWill.Topic );
            }

            if( Pumps?.IsRunning ?? false ) throw new InvalidOperationException( "This client is already connected." );

            var res = await DoConnectAsync( lastWill, cancellationToken );
            if( res.ConnectReturnCode == ConnectReturnCode.Accepted && ClientConfig.DisconnectBehavior == DisconnectBehavior.AutoReconnect )
            {
                _autoReconnectTask = ReconnectBackgroundAsync();
            }
            return res;
        }
        async Task<ConnectResult> DoConnectAsync( OutgoingLastWill? lastWill, CancellationToken cancellationToken )
        {
            try
            {
                var pumps = Pumps;
                if( pumps != null ) await pumps.DisposeAsync();
                if( ClientConfig.DisconnectBehavior == DisconnectBehavior.AutoReconnect )
                {
                    _disconnectTCS = new TaskCompletionSource();
                }

                // Creating pumps. Need to be started.
                OutputPump output = new( this );

                // Middleware that will processes the requests.
                ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder()
                    .UseMiddleware( new PublishReflex( this ) )
                    .UseMiddleware( new PublishLifecycleReflex( this ) )
                    .UseMiddleware( new SubackReflex( this ) )
                    .UseMiddleware( new UnsubackReflex( this ) );

                await Channel.StartAsync( cancellationToken ); // Will create the connection to server.

                // This reflex handle the connection packet.
                // It will replace itself with the regular packet processing.
                ConnectAckReflex connectAckReflex = new
                (
                    this,
                    builder.Build( this )
                );

                OutgoingConnect outgoingConnect = new( PConfig, ClientConfig, lastWill );

                WriteResult writeConnectResult;
                using( CancellationTokenSource cts = Config.TimeUtilities.CreateCTS( Config.WaitTimeoutMilliseconds ) )
                {
                    // Send the packet.
                    writeConnectResult = await outgoingConnect.WriteAsync( PConfig.ProtocolLevel, Channel.DuplexPipe.Output, cts.Token );
                    await Channel.DuplexPipe.Output.FlushAsync( cts.Token );
                }

                async ValueTask<ConnectResult> Exit( ConnectError connectError )
                {
                    Channel.Close();
                    var pumps = Pumps;
                    if( pumps != null )
                    {
                        await pumps.StopWorkAsync();
                        await pumps.DisposeAsync();
                    }
                    return new ConnectResult( connectError );
                }

                if( writeConnectResult != WriteResult.Written )
                    return await Exit( ConnectError.Timeout );
                var input = new InputPump( this, connectAckReflex.AsReflex );
                input.StartPumping();
                ConnectResult res;
                using( CancellationTokenSource cts2 = Config.TimeUtilities.CreateCTS( cancellationToken, Config.WaitTimeoutMilliseconds ) )
                using( cts2.Token.Register( () => connectAckReflex.TrySetCanceled( cancellationToken ) ) )
                {
                    try
                    {
                        res = await connectAckReflex.Task;
                    }
                    catch( OperationCanceledException )
                    {
                        return cancellationToken.IsCancellationRequested switch
                        {
                            true => await Exit( ConnectError.Connection_Cancelled ),
                            false => await Exit( ConnectError.Timeout )
                        };
                    }
                }

                OutputProcessor outputProcessor;
                // Enable keepalive only if we need it.
                if( ClientConfig.KeepAliveSeconds == 0 )
                {
                    outputProcessor = new OutputProcessor( this );
                }
                else
                {
                    // If keepalive is enabled, we add it's handler to the middlewares.
                    OutputProcessorWithKeepAlive withKeepAlive = new( this ); // Require channel started.
                    outputProcessor = withKeepAlive;
                    builder.UseMiddleware( withKeepAlive );
                }

                // When receiving the ConnAck, this reflex will replace the reflex with this property.
                Pumps = new( // Require channel started.
                    output,
                    input
                );
                output.StartPumping( outputProcessor ); // Start processing incoming messages.
                // This following code wouldn't be better with a sort of ... switch/pattern matching ?
                if( cancellationToken.IsCancellationRequested )
                    return await Exit( ConnectError.Connection_Cancelled );
                if( connectAckReflex.Task.Exception is not null || connectAckReflex.Task.IsFaulted )
                    return await Exit( ConnectError.InternalException );
                if( Pumps.IsClosed )
                    return await Exit( ConnectError.RemoteDisconnected );

                if( res.ConnectError != ConnectError.None )
                {
                    await Pumps.StopWorkAsync();
                    await Pumps.DisposeAsync();
                    Channel.Close();
                    return res;
                }

                bool askedCleanSession = ClientConfig.Credentials?.CleanSession ?? true;
                if( askedCleanSession && res.SessionState != SessionState.CleanSession )
                    return await Exit( ConnectError.ProtocolError_SessionNotFlushed );
                if( res.SessionState == SessionState.CleanSession )
                {
                    ValueTask task = RemotePacketStore.ResetAsync();
                    await LocalPacketStore.ResetAsync();
                    await task;
                }
                else
                {
                    throw new NotImplementedException();
                }
                Sink.Connected();
                return res;
            }
            catch( Exception )
            {
                // We may throw before the creation of the duplex pump.
                if( Pumps is not null )
                {
                    Channel.Close();
                    await Pumps.CloseAsync();
                    await Pumps.DisposeAsync();
                }
                return new ConnectResult( ConnectError.InternalException );
            }
        }


        internal protected override async ValueTask SelfDisconnectAsync( DisconnectReason disconnectedReason )
        {
            await base.SelfDisconnectAsync( disconnectedReason );
            _disconnectTCS?.TrySetResult(); //TrySet because we can have the user ask concurrently to Disconnect.
        }

        /// <inheritdoc/>
        public ValueTask<Task<SubscribeReturnCode>> SubscribeAsync( Subscription subscriptions )
        {
            MqttBinaryWriter.ThrowIfInvalidMQTTString( subscriptions.TopicFilter );
            return SendPacketWithQoSAsync<SubscribeReturnCode>( new OutgoingSubscribe( new[] { subscriptions } ) );
        }

        /// <inheritdoc/>
        public async ValueTask<Task> UnsubscribeAsync( params string[] topics )
        {
            foreach( string topic in topics )
            {
                MqttBinaryWriter.ThrowIfInvalidMQTTString( topic );
            }
            return await SendPacketWithQoSAsync<object>( new OutgoingUnsubscribe( topics ) );
        }


        /// <inheritdoc/>
        public ValueTask<Task<SubscribeReturnCode[]>> SubscribeAsync( IEnumerable<Subscription> subscriptions )
        {
            var subs = subscriptions.ToArray();
            foreach( Subscription sub in subs )
            {
                MqttBinaryWriter.ThrowIfInvalidMQTTString( sub.TopicFilter );
            }
            return SendPacketWithQoSAsync<SubscribeReturnCode[]>( new OutgoingSubscribe( subs ) )!;
        }


        CancellationTokenSource? _running;

        Task? _autoReconnectTask;
        TaskCompletionSource? _disconnectTCS;
        public async Task ReconnectBackgroundAsync()
        {
            await _disconnectTCS!.Task;
            await Pumps!.CloseAsync();
            _running = new();
            while( !_running.IsCancellationRequested )
            {
                ConnectResult result = await DoConnectAsync( null, _running.Token );
                if( result.ConnectError != ConnectError.None ) continue;
                await _disconnectTCS.Task;
                await Pumps!.CloseAsync();
            }
        }

        protected override async ValueTask BeforeFullDisconnectAsync( IDuplexPipe duplexPipe, bool clearSession )
        {
            _running?.Cancel();
            _disconnectTCS?.TrySetResult();
            var reconnectTask = _autoReconnectTask;
            if( reconnectTask != null ) await reconnectTask;

            if( clearSession )
            {
                using( CancellationTokenSource cts = Config.TimeUtilities.CreateCTS( Config.WaitTimeoutMilliseconds ) )
                {
                    await OutgoingDisconnect.Instance.WriteAsync( PConfig.ProtocolLevel, duplexPipe.Output, cts.Token );
                }
            }
        }

        public override async ValueTask DisposeAsync()
        {
            Channel.Dispose();
            await base.DisposeAsync();
            LocalPacketStore.Dispose();
            RemotePacketStore.Dispose();
        }
    }
}

