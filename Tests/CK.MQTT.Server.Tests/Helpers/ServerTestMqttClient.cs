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
        ServerTestMqttClient( 
                             Mqtt3ClientConfiguration config,
                             ChannelWriter<object?> eventWriter )
            : base( config, eventWriter )
        {
        }

        public ChannelReader<object?> ClientEvents { get; init; } = null!;

        public static ServerTestMqttClient Create(
            Mqtt3ClientConfiguration config )
        {
            Channel<object?> events = Channel.CreateUnbounded<object?>();
            return new ServerTestMqttClient(config,  events.Writer )
            {
                ClientEvents = events.Reader
            };
        }
    }
}
