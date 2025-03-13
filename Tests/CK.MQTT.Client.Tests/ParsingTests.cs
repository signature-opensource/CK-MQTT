using CK.MQTT.Client.Tests.Helpers;
using Shouldly;
using NUnit.Framework;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests;

public class ParsingTests_PipeReaderCop : ParsingTests
{
    public override string ClassCase => "PipeReaderCop";
}

public class ParsingTests_Default : ParsingTests
{
    public override string ClassCase => "Default";
}

public class ParsingTests_BytePerByteChannel : ParsingTests
{
    public override string ClassCase => "BytePerByte";
}
public abstract class ParsingTests
{
    public abstract string ClassCase { get; }

    [Test]
    public async Task can_read_maxsized_topic_Async()
    {
        StringBuilder sb = new StringBuilder()
            .Append( "30818004FFFF" );
        for( int i = 0; i < ushort.MaxValue; i++ )
        {
            sb.Append( "61" );
        }
        var replayer = new PacketReplayer( ClassCase );
        var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
        await replayer.ConnectClientAsync( TestHelper.Monitor, client );
        await replayer.SendToClientAsync( TestHelper.Monitor, sb.ToString() );
        await replayer.ShouldContainEventAsync<VolatileApplicationMessage>();
    }

    [Test]
    public async Task message_with_invalid_size_lead_to_protocol_error_Async()
    {
        var replayer = new PacketReplayer( ClassCase );
        var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
        await replayer.ConnectClientAsync( TestHelper.Monitor, client );

        await replayer.SendToClientAsync( TestHelper.Monitor, "308080808080000a7465737420746f70696374657374207061796c" );
        await replayer.ShouldContainEventAsync<LoopBackBase.ClosedChannel>();
        (await replayer
            .ShouldContainEventAsync<MQTTMessageSink.UnattendedDisconnect>())
            .Reason.ShouldBe( DisconnectReason.ProtocolError );
    }

    [Test]
    public async Task can_parse_5_messages_Async()
    {
        var replayer = new PacketReplayer( ClassCase );
        var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
        await replayer.ConnectClientAsync( TestHelper.Monitor, client );

        await replayer.SendToClientAsync( TestHelper.Monitor, "3018000a7465737420746f70696374657374207061796c6f6164" );
        await replayer.SendToClientAsync( TestHelper.Monitor, "3018000a7465737420746f70696374657374207061796c6f6164" );
        await replayer.SendToClientAsync( TestHelper.Monitor, "3018000a7465737420746f70696374657374207061796c6f6164" );
        await replayer.SendToClientAsync( TestHelper.Monitor, "3018000a7465737420746f70696374657374207061796c6f6164" );
        await replayer.SendToClientAsync( TestHelper.Monitor, "3018000a7465737420746f70696374657374207061796c6f6164" );
        await replayer.ShouldContainEventAsync<VolatileApplicationMessage>();
        await replayer.ShouldContainEventAsync<VolatileApplicationMessage>();
        await replayer.ShouldContainEventAsync<VolatileApplicationMessage>();
        await replayer.ShouldContainEventAsync<VolatileApplicationMessage>();
        await replayer.ShouldContainEventAsync<VolatileApplicationMessage>();
    }
}
