using CK.MQTT.Pumps;
using CK.MQTT.Server.Reflexes;
using CK.MQTT.Stores;

namespace CK.MQTT.Server
{
    public class ServerMessageExchanger : MessageExchanger
    {
        public string? ClientId { get; }
        public IMQTTServerSink ServerSink { get; }

        public ServerMessageExchanger(
            string? clientId,
            ProtocolConfiguration pConfig,
            MQTT3ConfigurationBase config,
            IMQTTServerSink sink,
            IMQTTChannel channel,
            IRemotePacketStore? remotePacketStore = null,
            ILocalPacketStore? localPacketStore = null
        ) : base( pConfig, config, sink, channel, remotePacketStore, localPacketStore )
        {
            ClientId = clientId;
            ServerSink = sink;
            Engage();
        }

        protected void Engage()
        {
            OutputPump = new OutputPump( Sink, Channel.DuplexPipe!.Output, PumpsDisconnectAsync, Config.OutgoingPacketsChannelCapacity );
            var outputProcessor = CreateOutputProcessor();
            OutputPump.OutputProcessor = outputProcessor;
            // Middleware that will processes the requests.
            ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder()
                .UseMiddleware( new PublishReflex( this ) )
                .UseMiddleware( new PublishLifecycleReflex( this ) )
                .UseMiddleware( new PingReqReflex( OutputPump ) )
                .UseMiddleware( new SubscribeReflex( ServerSink, PConfig.ProtocolLevel, OutputPump ) )
                .UseMiddleware( new UnsubscribeReflex( ServerSink, OutputPump, PConfig.ProtocolLevel ) );
            // When receiving the ConnAck, this reflex will replace the reflex with this property.
            Reflex reflex = builder.Build( PumpsDisconnectAsync );
            InputPump = CreateInputPump( reflex );
            // Creating pumps. Need to be started.
            OutputPump.StartPumping( StopToken, CloseToken );
            InputPump.StartPumping( StopToken, CloseToken );
        }

        protected virtual InputPump CreateInputPump( Reflex reflex ) => new( Sink, Channel.DuplexPipe!.Input, PumpsDisconnectAsync, reflex );

        protected virtual OutputProcessor CreateOutputProcessor() => new( Channel.DuplexPipe!.Output, PConfig, Config,  LocalPacketStore );
    }
}
