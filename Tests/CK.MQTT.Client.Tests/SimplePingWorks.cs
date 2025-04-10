using CK.MQTT.Client.Tests.Helpers;
using Shouldly;
using NUnit.Framework;
using System;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests;

public class PingTests_PipeReaderCop : PingTests
{
    public override string ClassCase => "PipeReaderCop";
}

public class PingTests_Default : PingTests
{
    public override string ClassCase => "Default";
}

public class PingTests_BytePerByteChannel : PingTests
{
    public override string ClassCase => "BytePerByte";
}

public abstract class PingTests
{
    public abstract string ClassCase { get; }

    [Test]
    public async Task normal_ping_works_Async()
    {
        var replayer = new PacketReplayer( ClassCase );
        var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfigWithKeepAlive( replayer ) );
        await replayer.ConnectClientAsync( TestHelper.Monitor, client );
        replayer.TestTimeHandler.IncrementTime( TimeSpan.FromSeconds( 4 ) );
        await Task.Delay( 1000 );
        replayer.Events.Reader.Count.ShouldBe( 0 );
        replayer.TestTimeHandler.IncrementTime( TimeSpan.FromSeconds( 5 ) );
        await replayer.AssertClientSentAsync( TestHelper.Monitor, "C0" );
    }

    [Test]
    public async Task ping_no_response_disconnect_Async()
    {
        var replayer = new PacketReplayer( ClassCase );
        var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfigWithKeepAlive( replayer ) );
        await replayer.ConnectClientAsync( TestHelper.Monitor, client );
        replayer.TestTimeHandler.IncrementTime( TimeSpan.FromSeconds( 5 ) );
        await replayer.AssertClientSentAsync( TestHelper.Monitor, "C0" );
        for( int i = 0; i < 5; i++ )
        {
            replayer.TestTimeHandler.IncrementTime( TimeSpan.FromSeconds( 6 ) );
            await Task.Delay( 5 );
        }
        await replayer.ShouldContainEventAsync<LoopBackBase.ClosedChannel>();
        var disconnect = await replayer.ShouldContainEventAsync<MQTTMessageSink.UnattendedDisconnect>();
        disconnect.Reason.ShouldBe( DisconnectReason.Timeout );
    }

    [Test]
    public async Task no_pings_sent_if_publish_Async()
    {
        var replayer = new PacketReplayer( ClassCase );
        var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfigWithKeepAlive( replayer ) );
        await replayer.ConnectClientAsync( TestHelper.Monitor, client );
        replayer.TestTimeHandler.IncrementTime( TimeSpan.FromSeconds( 4 ) );

        await Task.Delay( 100 );

        await await client.PublishAsync( new ApplicationMessage(
           "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.AtMostOnce, false )
       );
        await replayer.AssertClientSentAsync( TestHelper.Monitor, "3018000a7465737420746f70696374657374207061796c6f6164" );

        await Task.Delay( 100 );
        replayer.Events.Reader.Count.ShouldBe( 0 );
        replayer.TestTimeHandler.IncrementTime( TimeSpan.FromSeconds( 5 ) );
        await Task.Delay( 100 );
        await replayer.AssertClientSentAsync( TestHelper.Monitor, "C0" );
    }
}
