using CK.MQTT.Client.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests
{
    public class SubscribeTests_PipeReaderCop : SubscribeTests
    {
        public override string ClassCase => "PipeReaderCop";
    }

    public class SubscribeTests_Default : SubscribeTests
    {
        public override string ClassCase => "Default";
    }

    public class SubscribeTests_BytePerByteChannel : SubscribeTests
    {
        public override string ClassCase => "BytePerByte";
    }

    public abstract class SubscribeTests
    {
        public abstract string ClassCase { get; }

        [Test]
        public async Task simple_subscribe_works_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClientAsync( TestHelper.Monitor, client );

            var task = await client.SubscribeAsync( new Subscription( "test", QualityOfService.ExactlyOnce ) );
            await replayer.AssertClientSentAsync( TestHelper.Monitor, "8209000100047465737402" );
            await replayer.SendToClientAsync( TestHelper.Monitor, "9003000102" );
            (await task).Should().Be( SubscribeReturnCode.MaximumQoS2 );
        }
    }
}
