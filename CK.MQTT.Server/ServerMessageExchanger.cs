using CK.MQTT.Client;
using CK.MQTT.Common.Pumps;
using CK.MQTT.P2P;
using CK.MQTT.Pumps;
using CK.MQTT.Server.Reflexes;
using CK.MQTT.Stores;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    class ServerMessageExchanger : MessageExchanger
    {
        readonly ITopicManager _topicManager;

        public ServerMessageExchanger(
            ProtocolConfiguration pConfig,
            Mqtt3ConfigurationBase config,
            IMqtt3Sink sink,
            IMqttChannel channel,
            ITopicManager outgoingTopicManager,
            IRemotePacketStore? remotePacketStore = null,
            ILocalPacketStore? localPacketStore = null
        ) : base( pConfig, config, sink, channel, remotePacketStore, localPacketStore )
        {
            _topicManager = outgoingTopicManager;
        }

        protected void Engage()
        {
            var output = new OutputPump( this );
            // Middleware that will processes the requests.
            ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder()
                .UseMiddleware( new PublishReflex( this ) )
                .UseMiddleware( new PublishLifecycleReflex( this ) )
                .UseMiddleware( new SubscribeReflex( _topicManager, PConfig.ProtocolLevel, output ) )
                .UseMiddleware( new UnsubscribeReflex( _topicManager, output, PConfig.ProtocolLevel ) );
            // When receiving the ConnAck, this reflex will replace the reflex with this property.
            Reflex reflex = builder.Build( this );
            // Creating pumps. Need to be started.
            Pumps = new DuplexPump<OutputPump, InputPump>(
                output,
                CreateInputPump( reflex )
            );
            output.StartPumping( CreateOutputProcessor() );
        }

        protected virtual InputPump CreateInputPump( Reflex reflex ) => new( this, reflex );

        protected virtual OutputProcessor CreateOutputProcessor() => new( this );
    }
}
