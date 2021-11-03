using CK.Core;
using CK.MQTT.Client.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests
{
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
            TaskCompletionSource tcs = new();

            StringBuilder sb = new StringBuilder()
                .Append( "30818004FFFF" );
            for( int i = 0; i < ushort.MaxValue; i++ )
            {
                sb.Append( "61" );
            }
            (PacketReplayer packetReplayer, IMqtt3Client client) = await Scenario.ConnectedClient( ClassCase, new[]
            {
                TestPacketHelper.SendToClient(sb.ToString()),
            }, messageProcessor: ( m, msg, token ) =>
            {
                tcs.SetResult();
                msg.Dispose();
                return new ValueTask();
            } );
            await tcs.Task;
            await packetReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task partial_message_throws_with_application_message_closure()
        {
            TaskCompletionSource tcs = new();

            (PacketReplayer packetReplayer, IMqtt3Client client) = await Scenario.ConnectedClient( ClassCase, new[]
            {
                TestPacketHelper.SendToClient("3018000a7465737420746f70696374657374207061796c"),
                TestPacketHelper.Disconnect
            }, messageProcessor: ( IActivityMonitor? m, ApplicationMessage msg, CancellationToken token ) =>
            {
                Assert.Fail();
                return new ValueTask();
            }, ( reason ) =>
            {
                reason.Should().Be( DisconnectedReason.RemoteDisconnected );
                tcs.SetResult();
            } );
            (await tcs.Task.WaitAsync( 500 )).Should().BeTrue();
            await packetReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task partial_message_throws_with_disposable_application_message_closure()
        {
            TaskCompletionSource tcs = new();

            (PacketReplayer packetReplayer, IMqtt3Client client) = await Scenario.ConnectedClient( ClassCase, new[]
            {
                TestPacketHelper.SendToClient("3018000a7465737420746f70696374657374207061796c"),
                TestPacketHelper.Disconnect
            }, messageProcessor: ( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken token ) =>
            {
                Assert.Fail();
                return new ValueTask();
            }, ( reason ) =>
            {
                reason.Should().Be( DisconnectedReason.RemoteDisconnected );
                tcs.SetResult();
            } );
            (await tcs.Task.WaitAsync( 500 )).Should().BeTrue();
            await packetReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task can_parse_5_messages()
        {
            TaskCompletionSource tcs = new();
            int i = 0;
            (PacketReplayer packetReplayer, IMqtt3Client client) = await Scenario.ConnectedClient( ClassCase, new[]
            {
                TestPacketHelper.SendToClient("3018000a7465737420746f70696374657374207061796c6f6164"),
                TestPacketHelper.SendToClient("3018000a7465737420746f70696374657374207061796c6f6164"),
                TestPacketHelper.SendToClient("3018000a7465737420746f70696374657374207061796c6f6164"),
                TestPacketHelper.SendToClient("3018000a7465737420746f70696374657374207061796c6f6164"),
                TestPacketHelper.SendToClient("3018000a7465737420746f70696374657374207061796c6f6164"),
                TestPacketHelper.Disconnect
            }, messageProcessor: ( m, msg, token ) =>
            {
                if( ++i == 5 )
                {
                    tcs.SetResult();
                }
                msg.Dispose();
                return new ValueTask();
            } );
            (await tcs.Task.WaitAsync( 500 )).Should().BeTrue();
        }
    }
}
