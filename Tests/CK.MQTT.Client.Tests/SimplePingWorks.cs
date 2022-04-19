using CK.MQTT.Client.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests
{
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
        public async Task normal_ping_works()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfigWithKeepAlive( replayer ) );
            await replayer.ConnectClient( TestHelper.Monitor, client );
            replayer.TestTimeHandler.IncrementTime( TimeSpan.FromSeconds( 5 ) );
            await replayer.AssertClientSent( TestHelper.Monitor, "C0" );
        }

        [Test]
        public async Task ping_no_response_disconnect()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfigWithKeepAlive( replayer ) );
            await replayer.ConnectClient( TestHelper.Monitor, client );
            replayer.TestTimeHandler.IncrementTime( TimeSpan.FromSeconds( 5 ) );
            await replayer.AssertClientSent( TestHelper.Monitor, "C0" );
            for( int i = 0; i < 5; i++ )
            {
                replayer.TestTimeHandler.IncrementTime( TimeSpan.FromSeconds( 6 ) );
                await Task.Delay( 5 );
            }
            await replayer.ShouldContainEventAsync<LoopBackBase.ClosedChannel>();
            var disconnect = await replayer.ShouldContainEventAsync<TestMqttClient.UnattendedDisconnect>();
            disconnect.Reason.Should().Be( DisconnectReason.PingReqTimeout );
        }
    }
}
