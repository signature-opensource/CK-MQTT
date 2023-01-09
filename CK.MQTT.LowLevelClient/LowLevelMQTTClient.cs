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
        public async Task<ConnectResult> ConnectAsync( OutgoingLastWill? lastWill = null, CancellationToken cancellationToken = default )
        {
            if( lastWill != null ) MQTTBinaryWriter.ThrowIfInvalidMQTTString( lastWill.Topic );
            if( !StopTokenSource.IsCancellationRequested ) throw new InvalidOperationException( "This client is already connected." );

            _clientId = ClientConfig.Credentials?.ClientId;
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
                if( res.Status == ConnectStatus.Successful ) return Return( res, true ).Item1;
                Debug.Assert( res.Status != ConnectStatus.Deffered );
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
        async Task<ConnectResult> DoConnectAsync( OutgoingLastWill? lastWill, CancellationToken paramToken )
        {
            try
            {

                // http://web.archive.org/web/20160203062224/http://blogs.msdn.com/b/pfxteam/archive/2012/03/25/10287435.aspx
                // Disposing the CTS is not necessary and would involve locking, etc.
                Debug.Assert( StopTokenSource.IsCancellationRequested );
                StopTokenSource = new();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource( paramToken, StopTokenSource.Token );
                var token = cts.Token;
                if( ClientConfig.DisconnectBehavior == DisconnectBehavior.AutoReconnect )
                {
                    _disconnectTCS = new TaskCompletionSource<bool>();
                }


                // Middleware that will processes the requests.
                ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder()
                    .UseMiddleware( new PublishReflex( this ) )
                    .UseMiddleware( new PublishLifecycleReflex( this ) )
                    .UseMiddleware( new SubackReflex( this ) )
                    .UseMiddleware( new UnsubackReflex( this ) );

                await Channel.StartAsync( token ); // Will create the connection to server.
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

                using( CancellationTokenSource timeoutCts = Config.TimeUtilities.CreateCTS( token, Config.WaitTimeoutMilliseconds ) )
                {
                    // Send the packet.
                    await outgoingConnect.WriteAsync( PConfig.ProtocolLevel, Channel.DuplexPipe.Output, timeoutCts.Token );
                    await Channel.DuplexPipe.Output.FlushAsync( timeoutCts.Token );
                }

                // Creating pumps. Need to be started.
                InputPump = new InputPump( this, connectAckReflex.AsReflex );
                OutputPump = new OutputPump( this );
                InputPump.StartPumping();
                ConnectResult res;
                using( CancellationTokenSource cts2 = Config.TimeUtilities.CreateCTS( token, Config.WaitTimeoutMilliseconds ) )
                using( cts2.Token.Register( () => connectAckReflex.TrySetCanceled( cts2.Token ) ) )
                {
                    try
                    {
                        res = await connectAckReflex.Task.WaitAsync( cts2.Token );
                    }
                    catch( OperationCanceledException )
                    {
                        return cts2.Token.IsCancellationRequested ?
                             await ConnectExitAsync( ConnectError.Connection_Cancelled, DisconnectReason.UserDisconnected )
                            : await ConnectExitAsync( ConnectError.Timeout, DisconnectReason.Timeout );
                    }
                }

                OutputPump.StartPumping( outputProcessor ); // Start processing incoming messages.
                                                            // This following code wouldn't be better with a sort of ... switch/pattern matching ?
                bool askedCleanSession = ClientConfig.Credentials?.CleanSession ?? true;
                if( token.IsCancellationRequested )
                    return await ConnectExitAsync( ConnectError.Connection_Cancelled, DisconnectReason.UserDisconnected );
                if( connectAckReflex.Task.Exception is not null || connectAckReflex.Task.IsFaulted )
                    return await ConnectExitAsync( ConnectError.InternalException, DisconnectReason.InternalException );
                if( StopTokenSource.IsCancellationRequested )
                    return await ConnectExitAsync( ConnectError.RemoteDisconnected, DisconnectReason.RemoteDisconnected );
                if( res.Error != ConnectError.None )
                {

                    await CloseAsync( DisconnectReason.None );
                    return res;
                }
                if( askedCleanSession && res.SessionState != SessionState.CleanSession )
                    return await ConnectExitAsync( ConnectError.ProtocolError_SessionNotFlushed, DisconnectReason.ProtocolError );

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
                ClientSink.OnConnected();
                return res;
            }
            catch( Exception exception )
            {
                await CloseAsync( DisconnectReason.InternalException );
                return new ConnectResult( exception );
            }
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
            if( OutputPump!.WorkTask != null ) await OutputPump.WorkTask;
            if( InputPump!.WorkTask != null ) await InputPump.WorkTask;
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
                    ConnectResult result = await DoConnectAsync( null, _running.Token );
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

