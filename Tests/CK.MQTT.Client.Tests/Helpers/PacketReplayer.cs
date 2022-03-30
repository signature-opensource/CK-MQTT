using CK.Core;
using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    /// <summary>
    /// Allow to test the client with weird packet without hacking the server code.
    /// </summary>
    class PacketReplayer : IMqttChannelFactory
    {
        public Channel<object?> Events { get; } = System.Threading.Channels.Channel.CreateUnbounded<object?>();
        public LoopBack? Channel { get; private set; }
        public TestMqttClient Client { get; set; } = null!;
        public PacketReplayer( string channelType )
        {
            ChannelType = channelType;
        }
        public TestTimeHandler TestTimeHandler { get; } = new();
        public string ChannelType { get; set; }

        public delegate ValueTask<bool> ScenarioStep( IActivityMonitor m, PacketReplayer packetReplayer );

        public record CreatedChannel();
        public async ValueTask<IMqttChannel> CreateAsync( string connectionString )
        {
            // This must be done after the wait. The work in the loop may use the channel.
            Channel = ChannelType switch
            {
                "Default" => new DefaultLoopback( Events.Writer ),
                "BytePerByte" => new BytePerByteLoopback( Events.Writer ),
                "PipeReaderCop" => new PipeReaderCopLoopback( Events.Writer ),
                _ => throw new InvalidOperationException( "Unknown channel type." )
            };
            await Events.Writer.WriteAsync( new CreatedChannel() );
            return Channel;
        }
    }
}
