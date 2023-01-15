using CK.MQTT.Client;
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
    public sealed class LowLevelMQTTClient : MessageExchanger, IMQTT3Client
    {
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

        /// <inheritdoc/>
        public Task<ConnectResult> ConnectAsync( bool cleanSession, CancellationToken cancellationToken = default )
            => ConnectAsync( cleanSession, null, cancellationToken );

        /// <inheritdoc/>
        public async Task<ConnectResult> ConnectAsync( bool cleanSession, OutgoingLastWill? lastWill, CancellationToken cancellationToken = default )
        {

            if( !StopToken.IsCancellationRequested ) throw new InvalidOperationException( "This client is already connected." );
            Debug.Assert( CloseToken.IsCancellationRequested );
            try
            {
                await Channel.StartAsync( cancellationToken ); // Will create the connection to server.

                using( CancellationTokenSource timeoutCts = Config.TimeUtilities.CreateCTS( cancellationToken, Config.WaitTimeoutMilliseconds ) )
                {
                    // Send the packet.
                    var outgoingConnect = new OutgoingConnect( cleanSession, PConfig, ClientConfig, lastWill );
                    await outgoingConnect.WriteAsync( PConfig.ProtocolLevel, Channel.DuplexPipe.Output, timeoutCts.Token );
                    await Channel.DuplexPipe.Output.FlushAsync( timeoutCts.Token );
                }
                RenewTokens();
                ConnectResult res;
                using( CancellationTokenSource cts2 = Config.TimeUtilities.CreateCTS( cancellationToken, Config.WaitTimeoutMilliseconds ) )
                {
                    res = await ConnectResult.ReadAsync( Channel.DuplexPipe.Input, Sink, cts2.Token );
                    if( cts2.Token.IsCancellationRequested )
                    {
                        return cancellationToken.IsCancellationRequested ?
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
                    await DoDisconnectAsync( false, DisconnectReason.None );
                    return res;
                }


                //TODO: spaghetti setup here.

                // Middleware that will processes the requests.
                ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder()
                    .UseMiddleware( new PublishReflex( this ) )
                    .UseMiddleware( new PublishLifecycleReflex( this ) )
                    .UseMiddleware( new SubackReflex( this ) )
                    .UseMiddleware( new UnsubackReflex( this ) );

                OutputProcessor outputProcessor = BuildOutputProcessor( builder );
                // Creating pumps. Need to be started.
                InputPump = new InputPump( Sink, Channel.DuplexPipe.Input, PumpsDisconnectAsync, builder.Build( PumpsDisconnectAsync ) );
                OutputPump = new OutputPump( Sink, Channel.DuplexPipe.Output, PumpsDisconnectAsync, ClientConfig.OutgoingPacketsChannelCapacity );
                outputProcessor.OutputPump = OutputPump;
                OutputPump.OutputProcessor = outputProcessor;
                InputPump.StartPumping( StopToken, CloseToken );
                OutputPump.StartPumping( StopToken, CloseToken );

                ClientSink.OnConnected();
                outputProcessor.Starting();
                return res;
            }
            catch( Exception exception )
            {
                await DoDisconnectAsync( false, DisconnectReason.InternalException );
                return new ConnectResult( exception );
            }

            OutputProcessor BuildOutputProcessor( ReflexMiddlewareBuilder builder )
            {
                OutputProcessor outputProcessor;
                // Enable keepalive only if we need it.
                if( ClientConfig.KeepAliveSeconds == 0 )
                {
                    outputProcessor = new OutputProcessor( Channel.DuplexPipe.Output, PConfig, Config, LocalPacketStore );
                }
                else
                {
                    // If keepalive is enabled, we add it's handler to the middlewares.
                    var withKeepAlive = new OutputProcessorWithKeepAlive( Channel.DuplexPipe.Output, PConfig, ClientConfig, LocalPacketStore, PumpsDisconnectAsync );
                    outputProcessor = withKeepAlive;
                    builder.UseMiddleware( withKeepAlive );
                }
                return outputProcessor;
            }

            async ValueTask<ConnectResult> ConnectExitAsync( ConnectError connectError, DisconnectReason reason )
            {
                await DoDisconnectAsync( false, reason );
                return new ConnectResult( connectError );
            }


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

        protected override async ValueTask BeforeDisconnectAsync( IDuplexPipe duplexPipe, bool clearSession )
        {
            if( clearSession )
            {
                using( CancellationTokenSource cts = Config.TimeUtilities.CreateCTS( Config.WaitTimeoutMilliseconds ) )
                {
                    await OutgoingDisconnect.Instance.WriteAsync( PConfig.ProtocolLevel, duplexPipe.Output, cts.Token );
                }
            }
            await base.BeforeDisconnectAsync( duplexPipe, clearSession );
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            LocalPacketStore.Dispose();
            RemotePacketStore.Dispose();
        }
    }
}

