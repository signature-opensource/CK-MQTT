using CK.MQTT.Client;
using CK.MQTT.Common.Pumps;
using CK.MQTT.Pumps;
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
        public ServerMessageExchanger( ProtocolConfiguration pConfig, Mqtt3ConfigurationBase config, IMqtt3Sink sink, IMqttChannel channel, IRemotePacketStore? remotePacketStore = null, ILocalPacketStore? localPacketStore = null ) : base( pConfig, config, sink, channel, remotePacketStore, localPacketStore )
        {
        }

        protected void Engage()
        {
            var output = new OutputPump( Sink, LocalPacketStore, SelfDisconnectAsync, Config );
            // Middleware that will processes the requests.
            ReflexMiddlewareBuilder builder = new ReflexMiddlewareBuilder()
                .UseMiddleware( new PublishReflex( RemotePacketStore, OnMessageAsync, output ) )
                .UseMiddleware( new PublishLifecycleReflex( RemotePacketStore, LocalPacketStore, output ) )
                .UseMiddleware( new SubackReflex( LocalPacketStore ) )
                .UseMiddleware( new UnsubackReflex( LocalPacketStore ) );
            // When receiving the ConnAck, this reflex will replace the reflex with this property.
            Reflex reflex = builder.Build( async ( a, b, c, d, e, f ) =>
            {
                await SelfDisconnectAsync( DisconnectReason.ProtocolError );
                return OperationStatus.Done;
            } );
            // Creating pumps. Need to be started.
            Pumps = new DuplexPump<OutputPump, InputPump>(
                output,
                CreateInputPump( reflex )
            );
            output.StartPumping( new OutputProcessor( PConfig, output, Channel.DuplexPipe!.Output, LocalPacketStore ) );
        }

        protected virtual InputPump CreateInputPump( Reflex reflex )
            => new( Sink, SelfDisconnectAsync, Config, Channel.DuplexPipe!.Input, reflex );
    }
}
