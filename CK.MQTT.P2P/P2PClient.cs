using CK.MQTT.Common.Pumps;
using CK.MQTT.Packets;
using CK.MQTT.Pumps;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.P2P
{
    public class P2PClient : LowLevelMqttClientImpl
    {
        readonly IMqtt5ServerSink _sink;
        readonly ITopicFilter _topicFilter;
        readonly Channel<(bool, string[])> _subscriptionsCommand = Channel.CreateUnbounded<(bool, string[])>();
        internal P2PClient( IMqtt5ServerSink sink, P2PMqttConfiguration config )
            : base( sink, config )
        {
            _topicFilter = new TopicFilter();
            _sink = new SinkWrapper( sink, _topicFilter );
            P2PConfig = config;
        }

        class SinkWrapper : IMqtt5ServerSink
        {
            readonly IMqtt5ServerSink _sink;
            readonly ITopicFilter _topicFilter;
            public SinkWrapper( IMqtt5ServerSink sink, ITopicFilter topicFilter )
            {
                _sink = sink;
                _topicFilter = topicFilter;
            }

            public void Connected() => _sink.Connected();

            public void OnPacketResent( ushort packetId, int packetInTransitOrLost, bool isDropped ) => _sink.OnPacketResent( packetId, packetInTransitOrLost, isDropped );

            public void OnPacketWithDupFlagReceived( PacketType packetType ) => _sink.OnPacketWithDupFlagReceived( packetType );

            public void OnPoisonousPacket( ushort packetId, PacketType packetType, int poisonousTotalCount ) => _sink.OnPoisonousPacket( packetId, packetType, poisonousTotalCount );

            public void OnQueueFullPacketDropped( ushort packetId, PacketType packetType ) => _sink.OnQueueFullPacketDropped( packetId, packetType );

            public bool OnReconnectionFailed( int retryCount, int maxRetryCount ) => _sink.OnReconnectionFailed( retryCount, maxRetryCount );

            public void OnStoreFull( ushort freeLeftSlot ) => _sink.OnStoreFull( freeLeftSlot );

            public void OnUnattendedDisconnect( DisconnectReason reason ) => _sink.OnUnattendedDisconnect( reason );

            public void OnUnparsedExtraData( ushort packetId, ReadOnlySequence<byte> unparsedData ) => _sink.OnUnparsedExtraData( packetId, unparsedData );

            public async ValueTask ReceiveAsync( string topic, PipeReader reader, uint size, QualityOfService q, bool retain, CancellationToken cancellationToken )
            {
                if( _topicFilter.IsFiltered( topic ) )
                {
                    await reader.SkipBytesAsync( null, 0, size, cancellationToken );
                }
                else
                {
                    await _sink.ReceiveAsync( topic, reader, size, q, retain, cancellationToken );
                }
            }
        }


        public P2PMqttConfiguration P2PConfig { get; }
        public override async Task<ConnectResult> ConnectAsync( OutgoingLastWill? lastWill = null, CancellationToken cancellationToken = default )
        {
            if( lastWill != null ) throw new ArgumentException( "Last will is not supported by a P2P client." );
            if( Config.KeepAliveSeconds != 0 ) throw new NotSupportedException( "Server KeepAlive is not yet supported." );
            if( Pumps?.IsRunning ?? false ) throw new InvalidOperationException( "This client is already connected." );
            try
            {
                IMqttChannel channel = await P2PConfig.ChannelFactory.CreateAsync( P2PConfig.ConnectionString );

                ConnectReflex connectReflex = new( _sink, P2PConfig.ProtocolConfiguration, P2PConfig );
                // Creating pumps. Need to be started.
                var inputPump = new InterlacedInputPump(
                    _sink,
                    _topicFilter,
                    SelfDisconnectAsync, Config, channel.DuplexPipe.Input, connectReflex.HandleRequestAsync,
                    _subscriptionsCommand.Reader
                );

                await connectReflex.ConnectHandledTask;

                OutputPump output = new( _sink, connectReflex.OutStore, SelfDisconnectAsync, Config );

                // This reflex handle the connection packet.
                // It will replace itself with the regular packet processing.

                Pumps = new DuplexPump<ClientState>(
                    new ClientState( output, channel ),
                    output,
                    inputPump
                );

                // Middleware that will processes the requests.
                ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder()
                    .UseMiddleware( new PublishReflex( Config, connectReflex.InStore, OnMessageAsync, output ) )
                    .UseMiddleware( new PublishLifecycleReflex( connectReflex.InStore, connectReflex.OutStore, output ) )
                    .UseMiddleware( new SubackReflex( connectReflex.OutStore ) )
                    .UseMiddleware( new UnsubackReflex( connectReflex.OutStore ) );
                var outputProcessor = new FiltringOutputProcessor( _topicFilter, P2PConfig.ProtocolConfiguration, output, channel.DuplexPipe.Output, connectReflex.OutStore );
                // Enable keepalive only if we need it.

                // When receiving the ConnAck, this reflex will replace the reflex with this property.
                Reflex reflex = builder.Build( async ( a, b, c, d, e, f ) =>
                {
                    await SelfDisconnectAsync( DisconnectReason.ProtocolError );
                    return OperationStatus.Done;
                } );
                connectReflex.EngageNextReflex( reflex );
                await channel.StartAsync(); // Will create the connection to server.
                output.StartPumping( outputProcessor ); // Start processing incoming messages.


                if( Pumps.IsClosed )
                {
                    await Pumps!.DisposeAsync();
                    return new ConnectResult( ConnectError.RemoteDisconnected );
                }

                bool hasExistingSession = connectReflex.OutStore.IsRevivedSession || connectReflex.InStore.IsRevivedSession;
                await output.QueueMessageAndWaitUntilSentAsync( new ConnectAckPacket( hasExistingSession, ConnectReturnCode.Accepted ) );

                return new ConnectResult( hasExistingSession ? SessionState.SessionPresent : SessionState.CleanSession, ConnectReturnCode.Accepted );
            }
            catch( Exception )
            {
                // We may throw before the creation of the duplex pump.
                if( Pumps is not null ) await Pumps.DisposeAsync(); ;
                return new ConnectResult( ConnectError.InternalException );
            }
        }


        public override async ValueTask<Task<SubscribeReturnCode>> SubscribeAsync( Subscription subscriptions )
        {
            await _subscriptionsCommand.Writer.WriteAsync( (true, new string[] { subscriptions.TopicFilter }) );
            return Task.FromResult(
                SubscribeReturnCode.MaximumQoS0
            // We cannot ask the client a certain QoS as it's a fake subscribe, we return 0 because we canno't make the guarentee the QoS will be higher.
            );
        }

        public override async ValueTask<Task<SubscribeReturnCode[]>> SubscribeAsync( IEnumerable<Subscription> subscriptions )
        {
            var subs = subscriptions.Select( s => s.TopicFilter ).ToArray();
            await _subscriptionsCommand.Writer.WriteAsync( (true, subs) );
            return Task.FromResult(
                new SubscribeReturnCode[subs.Length]
            // We cannot ask the client a certain QoS as it's a fake subscribe, we return 0 because we canno't make the guarentee the QoS will be higher.
            );
        }

        public override async ValueTask<Task> UnsubscribeAsync( params string[] topics )
        {
            await _subscriptionsCommand.Writer.WriteAsync( (false, topics) );
            return Task.CompletedTask;
        }
    }
}
