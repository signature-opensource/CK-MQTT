using CK.MQTT.Client;
using CK.MQTT.Packets;
using CK.MQTT.Pumps;
using CK.MQTT.Stores;
using CommunityToolkit.HighPerformance.Helpers;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Linq.Expressions;
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

            while( true )
            {
                var res = await DoConnectAsync( lastWill, cancellationToken );
                (ConnectResult, bool) Return( ConnectResult result, bool success )
                {
                    if( success && ClientConfig.DisconnectBehavior == DisconnectBehavior.AutoReconnect )
                    {
                        _autoReconnectTask = ReconnectBackgroundAsync();
                    }
                    return (result, true);
                }
                static (ConnectResult, bool) Retry( ConnectResult result ) => (result, false);

                Debug.Assert( res.Status != ConnectStatus.Deffered );
                var sinkBehavior = Sink.OnFailedManualConnect( res );
                var configBehavior = ClientConfig.ManualConnectBehavior;
                (ConnectResult connectResult, bool stopRetries) = res.Status switch
                {
                    ConnectStatus.Successful => Return( res, true ),
                    _ => configBehavior switch
                    {
                        ManualConnectBehavior.TryOnce => Return( res, true ),
                        ManualConnectBehavior.UseSinkBehavior => sinkBehavior switch
                        {
                            IMqtt3Sink.ManualConnectRetryBehavior.GiveUp => Return( res, res.Status == ConnectStatus.Successful ),
                            IMqtt3Sink.ManualConnectRetryBehavior.YieldToBackground =>
                                (ClientConfig.DisconnectBehavior != DisconnectBehavior.AutoReconnect) switch
                                {
                                    true => throw new ArgumentException(
                                        $"The configuration is set to {ClientConfig.DisconnectBehavior}, " +
                                        $"but the {Sink} asked to yield the reconnect to the AutoReconnect loop."
                                    ),
                                    false => Return( new ConnectResult( true, res.Error, res.SessionState, res.ProtocolReturnCode, res.Exception ), true )
                                },
                            IMqtt3Sink.ManualConnectRetryBehavior.Retry => Retry( res ),
                            _ => throw new InvalidOperationException( $"Invalid {nameof( IMqtt3Sink.ManualConnectRetryBehavior )}:{sinkBehavior}" )
                        },
                        ManualConnectBehavior.TryOnceThenRetryInBackground when ClientConfig.DisconnectBehavior != DisconnectBehavior.AutoReconnect => throw new ArgumentException( $"Cannot use {ManualConnectBehavior.TryOnceThenRetryInBackground} when {nameof( DisconnectBehavior )} is not set to {DisconnectBehavior.AutoReconnect}.." ),
                        ManualConnectBehavior.TryOnceThenRetryInBackground => Return( new ConnectResult( true, res.Error, res.SessionState, res.ProtocolReturnCode, res.Exception ), true ),
                            ManualConnectBehavior.RetryUntilConnectedOrUnrecoverable when res.Status == ConnectStatus.ErrorUnknown => Return( res, false ),
                        ManualConnectBehavior.RetryUntilConnectedOrUnrecoverable when res.Status == ConnectStatus.ErrorUnrecoverable => Return( res, false ),
                        ManualConnectBehavior.RetryUntilConnectedOrUnrecoverable => Retry( res ),
                        ManualConnectBehavior.RetryUntilConnected => Retry( res ),
                        _ => throw new InvalidOperationException( $"Invalid {nameof( ManualConnectBehavior )}:{configBehavior}." )
                    }
                };
                if( stopRetries ) return connectResult;
            }

        }
        async Task<ConnectResult> DoConnectAsync( OutgoingLastWill? lastWill, CancellationToken cancellationToken )
        {
            try
            {
                if( ClientConfig.DisconnectBehavior == DisconnectBehavior.AutoReconnect )
                {
                    _disconnectTCS = new TaskCompletionSource<bool>();
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
                // This reflex handle the connection packet.
                // It will replace itself with the regular packet processing.
                ConnectAckReflex connectAckReflex = new
                (
                    this,
                    builder.Build()
                );

                OutgoingConnect outgoingConnect = new( PConfig, ClientConfig, lastWill );

                using( CancellationTokenSource cts = Config.TimeUtilities.CreateCTS( Config.WaitTimeoutMilliseconds ) )
                {
                    // Send the packet.
                    await outgoingConnect.WriteAsync( PConfig.ProtocolLevel, Channel.DuplexPipe.Output, cts.Token );
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

                if( res.Error != ConnectError.None )
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
            catch( Exception exception )
            {
                // We may throw before the creation of the duplex pump.
                if( Pumps is not null )
                {
                    Channel.Close();
                    await Pumps.CloseAsync();
                    await Pumps.DisposeAsync();
                }
                return new ConnectResult( exception );
            }
        }


        /// <inheritdoc/>
        internal protected override async ValueTask<bool> SelfDisconnectAsync( DisconnectReason disconnectedReason )
        {
            var shouldReconnect = await base.SelfDisconnectAsync( disconnectedReason );
            //TrySetResult because we can have the user ask concurrently to Disconnect.
            _disconnectTCS?.TrySetResult( shouldReconnect );
            return shouldReconnect;
        }

        /// <inheritdoc/>
        public async ValueTask<Task<SubscribeReturnCode>> SubscribeAsync( Subscription subscriptions )
        {
            MqttBinaryWriter.ThrowIfInvalidMQTTString( subscriptions.TopicFilter );
            var sendTask = await SendPacketWithQoSAsync<SubscribeReturnCode[]>( new OutgoingSubscribe( new[] { subscriptions } ) );
            return Send( sendTask! );

            static async Task<SubscribeReturnCode> Send( Task<SubscribeReturnCode[]> task )
            {
                var res = await task;
                return res?[0] ?? SubscribeReturnCode.Failure;
            }
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
        TaskCompletionSource<bool>? _disconnectTCS;
        public async Task ReconnectBackgroundAsync()
        {
            var shouldReconnect = await _disconnectTCS!.Task;
            if( !shouldReconnect ) return;
            await Pumps!.CloseAsync();
            using( _running = new() )
            {
                while( !_running.IsCancellationRequested && shouldReconnect )
                {
                    ConnectResult result = await DoConnectAsync( null, _running.Token );
                    if( result.Error != ConnectError.None )
                    {
                        shouldReconnect = await Sink.OnReconnectionFailedAsync( result );
                        if( !shouldReconnect ) return;
                    }
                    shouldReconnect = await _disconnectTCS.Task;
                    await Pumps!.CloseAsync();
                }
            }
        }

        protected override async ValueTask BeforeUserDisconnectAsync( IDuplexPipe duplexPipe, bool clearSession )
        {
            _running?.Cancel();
            _disconnectTCS?.TrySetResult( false );
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

