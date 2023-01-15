using CK.MQTT.Client;
using System.Threading.Channels;

namespace CK.MQTT.Server.Tests.Helpers
{
    class ServerTestMQTTClient : TestMQTTClient
    {
        ServerTestMQTTClient( ProtocolConfiguration pConfig,
                             MQTT3ClientConfiguration config,
                             IMQTTChannel channel,
                             ChannelWriter<object?> eventWriter )
            : base( pConfig, config, channel, eventWriter )
        {
        }

        public ChannelReader<object?> ClientEvents { get; init; } = null!;

        public static ServerTestMQTTClient Create(
            ProtocolConfiguration pConfig,
            MQTT3ClientConfiguration config,
            IMQTTChannel channel )
        {
            Channel<object?> events = Channel.CreateUnbounded<object?>();
            return new ServerTestMQTTClient( pConfig, config, channel, events.Writer )
            {
                ClientEvents = events.Reader
            };
        }
    }
}
