using CK.MQTT.Client.Middleware;
using CK.MQTT.Client.Tests.Helpers;
using Shouldly;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests;

public class AutoReconnectTests_PipeReaderCop : AutoReconnectTests
{
    public override string ClassCase => "PipeReaderCop";
}

public class AutoReconnectTests_Default : AutoReconnectTests
{
    public override string ClassCase => "Default";
}

public class AutoReconnectTests_BytePerByteChannel : AutoReconnectTests
{
    public override string ClassCase => "BytePerByte";
}
public abstract class AutoReconnectTests
{
    public abstract string ClassCase { get; }

    [Test]
    public async Task auto_reconnect_works_Async()
    {
        var replayer = new PacketReplayer( ClassCase );
        var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfigWithKeepAlive( replayer ), withReconnect: true );
        await replayer.ConnectClientAsync( TestHelper.Monitor, client );
        //Doesn't reconnect if the first connect fails.
        replayer.TestTimeHandler.IncrementTime( TimeSpan.FromSeconds( 6 ) );
        await Task.Delay( 100 );
        replayer.TestTimeHandler.IncrementTime( TimeSpan.FromSeconds( 6 ) );
        await replayer.ShouldContainEventAsync<LoopBackBase.ClosedChannel>();
        await replayer.ShouldContainEventAsync<MQTTMessageSink.UnattendedDisconnect>();
        await replayer.ShouldContainEventAsync<HandleAutoReconnect.AutoReconnectAttempt>();
        await replayer.ShouldContainEventAsync<LoopBackBase.StartedChannel>();

        await replayer.AssertClientSentAsync( TestHelper.Monitor, "101600044d5154540400" + Convert.ToHexString( BitConverter.GetBytes( replayer.Config.KeepAliveSeconds ).Reverse().ToArray() ) + "000a434b4d71747454657374" );
        await replayer.SendToClientAsync( TestHelper.Monitor, "20020000" );
        await Task.Delay( 100 );
        await replayer.ShouldContainEventAsync<DefaultClientMessageSink.Connected>();
        replayer.Events.Reader.Count.ShouldBe( 0 );
    }
}
