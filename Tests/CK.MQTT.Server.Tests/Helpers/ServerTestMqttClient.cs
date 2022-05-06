using CK.MQTT.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Server.Tests.Helpers
{
    class ServerTestMqttClient : TestMqttClient
    {
        ServerTestMqttClient( ProtocolConfiguration pConfig,
                             Mqtt3ClientConfiguration config,
                             IMqttChannel channel,
                             ChannelWriter<object?> eventWriter )
            : base( pConfig, config, channel, eventWriter )
        {
        }

        public ChannelReader<object?> ClientEvents { get; init; } = null!;

        public static ServerTestMqttClient Create(
            ProtocolConfiguration pConfig,
            Mqtt3ClientConfiguration config,
            IMqttChannel channel )
        {
            Channel<object?> events = Channel.CreateUnbounded<object?>();
            return new ServerTestMqttClient( pConfig, config, channel, events.Writer )
            {
                ClientEvents = events.Reader
            };
        }
    }
}
