using CK.MQTT.Client.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests
{
    public class ParsingTests_PipeReaderCop : ConnectionTests
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
        public async Task can_read_maxsized_topic()
        {
            StringBuilder sb = new StringBuilder()
                .Append( "30818004FFFF" );
            for( int i = 0; i < ushort.MaxValue; i++ )
            {
                sb.Append( "61" );
            }
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClient( TestHelper.Monitor, client );
            await replayer.SendToClient( TestHelper.Monitor, sb.ToString() );
            await replayer.ShouldContainEventAsync<ApplicationMessage>();
        }

        [Test]
        public async Task message_with_invalid_size_lead_to_protocol_error()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClient( TestHelper.Monitor, client );

            await replayer.SendToClient( TestHelper.Monitor, "308080808080000a7465737420746f70696374657374207061796c" );
            await replayer.ShouldContainEventAsync<LoopBack.DisposedChannel>();
            (await replayer
                .ShouldContainEventAsync<TestMqttClient.UnattendedDisconnect>())
                .Reason.Should().Be( DisconnectReason.ProtocolError );
        }

        [Test]
        public async Task can_parse_5_messages()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClient( TestHelper.Monitor, client );

            await replayer.SendToClient( TestHelper.Monitor, "3018000a7465737420746f70696374657374207061796c6f6164" );
            await replayer.SendToClient( TestHelper.Monitor, "3018000a7465737420746f70696374657374207061796c6f6164" );
            await replayer.SendToClient( TestHelper.Monitor, "3018000a7465737420746f70696374657374207061796c6f6164" );
            await replayer.SendToClient( TestHelper.Monitor, "3018000a7465737420746f70696374657374207061796c6f6164" );
            await replayer.SendToClient( TestHelper.Monitor, "3018000a7465737420746f70696374657374207061796c6f6164" );
            await replayer.ShouldContainEventAsync<ApplicationMessage>();
            await replayer.ShouldContainEventAsync<ApplicationMessage>();
            await replayer.ShouldContainEventAsync<ApplicationMessage>();
            await replayer.ShouldContainEventAsync<ApplicationMessage>();
            await replayer.ShouldContainEventAsync<ApplicationMessage>();
        }
    }
}
