using CK.Core;
using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers;

/// <summary>
/// Allow to test the client with weird packet without hacking the server code.
/// </summary>
class PacketReplayer
{
    public Channel<object?> Events { get; } = System.Threading.Channels.Channel.CreateUnbounded<object?>();
    public LoopBackBase? Channel { get; private set; }
    public MQTTClientAgent Client { get; set; } = null!;
    public PacketReplayer( string channelType )
    {
        ChannelType = channelType;
    }
    public TestTimeHandler TestTimeHandler { get; } = new();
    public string ChannelType { get; set; }
    public MQTT3ClientConfiguration Config { get; internal set; } = null!;

    public delegate ValueTask<bool> ScenarioStep( IActivityMonitor m, PacketReplayer packetReplayer );

    public IMQTTChannel CreateChannel( Action<object?> messageWriter )
    {
        // This must be done after the wait. The work in the loop may use the channel.
        Channel = ChannelType switch
        {
            "Default" => new DefaultLoopback( messageWriter ),
            "BytePerByte" => new BytePerByteLoopback( messageWriter ),
            "PipeReaderCop" => new PipeReaderCopLoopback( messageWriter ),
            _ => throw new InvalidOperationException( "Unknown channel type." )
        };
        return Channel;
    }
}
