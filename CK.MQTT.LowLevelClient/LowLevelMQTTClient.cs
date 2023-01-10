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
    public sealed class LowLevelMQTTClient : MessageExchanger, IMQTT3Client
    {
        CancellationTokenSource? _running;

        Task? _autoReconnectTask;
        TaskCompletionSource<bool>? _disconnectTCS;
        string? _clientId;

        public MQTT3ClientConfiguration ClientConfig { get; }
        public IMQTT3ClientSink ClientSink { get; }

        public LowLevelMQTTClient(
            ProtocolConfiguration pConfig,
            MQTT3ClientConfiguration config,
            IMQTT3ClientSink sink,
            IMQTTChannel channel,
            IRemotePacketStore? remotePacketStore = null,
            ILocalPacketStore? localPacketStore = null
        ) : base( pConfig, config, sink, channel, remotePacketStore, localPacketStore )
        {
            if( config.WaitTimeoutMilliseconds > config.KeepAliveSeconds * 1000 && config.KeepAliveSeconds != 0 )
            {
                throw new ArgumentException( "Wait timeout should be smaller than the keep alive." );
            }
            ClientConfig = config;
            ClientSink = sink;
            sink.Client = this;
        }

        public override string? ClientId => _clientId;

        /// <inheritdoc/>
        public Task<ConnectResult> ConnectAsync( bool cleanSession, CancellationToken cancellationToken = default )
            => ConnectAsync( cleanSession, null, cancellationToken );

        /// <inheritdoc/>
        public async Task<ConnectResult> ConnectAsync( bool cleanSession, OutgoingLastWill? lastWill, CancellationToken cancellationToken = default )
        {
            if( lastWill != null ) MQTTBinaryWriter.ThrowIfInvalidMQTTString( lastWill.Topic );
            if( !StopTokenSource.IsCancellationRequested ) throw new InvalidOperationException( "This client is already connected." );

            _clientId = ClientConfig.ClientId;
            while( true )
            {
                var res = await DoConnectAsync( cleanSession, lastWill, cancellationToken );
                (ConnectResult, bool) Return( ConnectResult result, bool success )
                {
                    if( success && ClientConfig.DisconnectBehavior == DisconnectBehavior.AutoReconnect )
                    {
                        _autoReconnectTask = ReconnectBackgroundAsync();
                    }
                    return (result, true);
                }
                static (ConnectResult, bool) Retry( ConnectResult result ) => (result, false);
                if( res.Status == ConnectStatus.Successful ) return Return( res, true ).Item1;
                Debug.Assert( res.Status != ConnectStatus.Deferred );
                var sinkBehavior = ClientSink.OnFailedManualConnect( res );
                var configBehavior = ClientConfig.ManualConnectBehavior;
                (ConnectResult connectResult, bool stopRetries) = res.Status switch
                {
                    ConnectStatus.Successful => Return( res, true ),
                    _ => configBehavior switch
                    {
                        ManualConnectBehavior.TryOnce => Return( res, true ),
                        ManualConnectBehavior.UseSinkBehavior => sinkBehavior switch
                        {
                            IMQTT3ClientSink.ManualConnectRetryBehavior.GiveUp => Return( res, res.Status == ConnectStatus.Successful ),
                            IMQTT3ClientSink.ManualConnectRetryBehavior.YieldToBackground =>
                                (ClientConfig.DisconnectBehavior != DisconnectBehavior.AutoReconnect) switch
                                {
                                    true => throw new ArgumentException(
                                        $"The configuration is set to {ClientConfig.DisconnectBehavior}, " +
                                        $"but the {Sink} asked to yield the reconnect to the AutoReconnect loop."
                                    ),
                                    false => Return( new ConnectResult( true, res.Error, res.SessionState, res.ProtocolReturnCode, res.Exception ), true )
                                },
                            IMQTT3ClientSink.ManualConnectRetryBehavior.Retry => Retry( res ),
                            _ => throw new InvalidOperationException( $"Invalid {nameof( IMQTT3ClientSink.ManualConnectRetryBehavior )}:{sinkBehavior}" )
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
        async Task<ConnectResult> DoConnectAsync( bool cleanSession, OutgoingLastWill? lastWill, CancellationToken paramToken )
        {
            try
            {
                if( ClientConfig.DisconnectBehavior == DisconnectBehavior.AutoReconnect )
                {
                    _disconnectTCS = new TaskCompletionSource<bool>();
                }

                await Channel.StartAsync( paramToken ); // Will create the connection to server.

                using( CancellationTokenSource timeoutCts = Config.TimeUtilities.CreateCTS( paramToken, Config.WaitTimeoutMilliseconds ) )
                {
                    // Send the packet.
                    var outgoingConnect = new OutgoingConnect( cleanSession, PConfig, ClientConfig, lastWill );
                    await outgoingConnect.WriteAsync( PConfig.ProtocolLevel, Channel.DuplexPipe.Output, timeoutCts.Token );
                    await Channel.DuplexPipe.Output.FlushAsync( timeoutCts.Token );
                }
                ConnectResult res;
                using( CancellationTokenSource cts2 = Config.TimeUtilities.CreateCTS( paramToken, Config.WaitTimeoutMilliseconds ) )
                {
                    res = await ConnectResult.ReadAsync( Channel.DuplexPipe.Input, Sink, cts2.Token );
                    if( cts2.Token.IsCancellationRequested )
                    {
                        return paramToken.IsCancellationRequested ?
                           await ConnectExitAsync( ConnectError.UserCancelled, DisconnectReason.UserDisconnected )
                          : await ConnectExitAsync( ConnectError.Timeout, DisconnectReason.Timeout );
                    }
                }

                if( cleanSession && res.SessionState != SessionState.CleanSession )
                    return await ConnectExitAsync( ConnectError.ProtocolError, DisconnectReason.ProtocolError );

                if( res.SessionState == SessionState.CleanSession )
                {
                    ValueTask task = RemotePacketStore.ResetAsync();
                    await LocalPacketStore.ResetAsync();
                    await task;
                }

                if( res.Error != ConnectError.None )
                {
                    await CloseAsync( DisconnectReason.None );
                    return res;
                }


                Debug.Assert( StopTokenSource.IsCancellationRequested );
                // http://web.archive.org/web/20160203062224/http://blogs.msdn.com/b/pfxteam/archive/2012/03/25/10287435.aspx
                // Disposing the CTS is not necessary and would involve locking, etc.
                StopTokenSource = new();

                using var cts = CancellationTokenSource.CreateLinkedTokenSource( paramToken, StopTokenSource.Token );
                var token = cts.Token;

                // Middleware that will processes the requests.
                ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder()
                    .UseMiddleware( new PublishReflex( this ) )
                    .UseMiddleware( new PublishLifecycleReflex( this ) )
                    .UseMiddleware( new SubackReflex( this ) )
                    .UseMiddleware( new UnsubackReflex( this ) );
                OutputProcessor outputProcessor = BuildOutputProcessor( builder );

               

                // Creating pumps. Need to be started.
                InputPump = new InputPump( this, builder.Build() );
                OutputPump = new OutputPump( this );
                InputPump.StartPumping();
                OutputPump.StartPumping( outputProcessor );

                ClientSink.OnConnected();
                return res;
            }
            catch( Exception exception )
            {
                await CloseAsync( DisconnectReason.InternalException );
                return new ConnectResult( exception );
            }
        }

        private OutputProcessor BuildOutputProcessor( ReflexMiddlewareBuilder builder )
        {
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

            return outputProcessor;
        }

        async ValueTask<ConnectResult> ConnectExitAsync( ConnectError connectError, DisconnectReason reason )
        {
            await CloseAsync( reason );
            return new ConnectResult( connectError );
        }

        async ValueTask CloseAsync( DisconnectReason reason )
        {
            StopTokenSource.Cancel();
            await Channel.CloseAsync( reason );
            if( OutputPump?.WorkTask != null ) await OutputPump.WorkTask;
            if( InputPump?.WorkTask != null ) await InputPump.WorkTask;
        }



        /// <inheritdoc/>
        internal protected override async ValueTask<bool> FinishSelfDisconnectAsync( DisconnectReason disconnectedReason )
        {
            var shouldReconnect = await base.FinishSelfDisconnectAsync( disconnectedReason );
            //TrySetResult because we can have the user ask concurrently to Disconnect.
            _disconnectTCS?.TrySetResult( shouldReconnect );
            return shouldReconnect;
        }

        /// <inheritdoc/>
        public async ValueTask<Task<SubscribeReturnCode>> SubscribeAsync( Subscription subscriptions )
        {
            MQTTBinaryWriter.ThrowIfInvalidMQTTString( subscriptions.TopicFilter );
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
                MQTTBinaryWriter.ThrowIfInvalidMQTTString( topic );
            }
            return await SendPacketWithQoSAsync<object>( new OutgoingUnsubscribe( topics ) );
        }


        /// <inheritdoc/>
        public ValueTask<Task<SubscribeReturnCode[]>> SubscribeAsync( IEnumerable<Subscription> subscriptions )
        {
            var subs = subscriptions.ToArray();
            foreach( Subscription sub in subs )
            {
                MQTTBinaryWriter.ThrowIfInvalidMQTTString( sub.TopicFilter );
            }
            return SendPacketWithQoSAsync<SubscribeReturnCode[]>( new OutgoingSubscribe( subs ) )!;
        }




        public async Task ReconnectBackgroundAsync()
        {
            var shouldReconnect = await _disconnectTCS!.Task;
            if( !shouldReconnect ) return;
            using( _running = new() )
            {
                while( !_running.IsCancellationRequested && shouldReconnect )
                {
                    ConnectResult result = await DoConnectAsync( false, null, _running.Token );
                    if( result.Error != ConnectError.None )
                    {
                        shouldReconnect = await ClientSink.OnReconnectionFailedAsync( result );
                        if( !shouldReconnect ) return;
                    }
                    shouldReconnect = await _disconnectTCS.Task;
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
            await base.DisposeAsync();
            LocalPacketStore.Dispose();
            RemotePacketStore.Dispose();
        }
    }
}

