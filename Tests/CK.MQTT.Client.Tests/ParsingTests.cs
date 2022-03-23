using CK.Core;
using CK.MQTT.Client.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests
{
    [ExcludeFromCodeCoverage]
    public class ParsingTests_PipeReaderCop : ConnectionTests
    {
        public override string ClassCase => "PipeReaderCop";
    }

    [ExcludeFromCodeCoverage]
    public class ParsingTests_Default : ParsingTests
    {
        public override string ClassCase => "Default";
    }

    [ExcludeFromCodeCoverage]
    public class ParsingTests_BytePerByteChannel : ParsingTests
    {
        public override string ClassCase => "BytePerByte";
    }
    [ExcludeFromCodeCoverage]
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
            (PacketReplayer packetReplayer, SimpleTestMqtt3Client client) = await Scenario.ConnectedClient( ClassCase, new[]
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

        //[Test]
        //public async Task partial_message_throws_with_application_message_closure()
        //{
        //    TaskCompletionSource<DisconnectReason> tcs = new();

        //    (PacketReplayer packetReplayer, SimpleTestMqtt3Client client) = await Scenario.ConnectedClient( ClassCase, new[]
        //    {
        //        TestPacketHelper.SendToClient("3018000a7465737420746f70696374657374207061796c"),
        //        TestPacketHelper.Disconnect
        //    }, messageProcessor: ( IActivityMonitor? m, ApplicationMessage msg, CancellationToken token ) =>
        //    {
        //        tcs.TrySetException( new AssertionException( "We shouldn't receive a message in this scenario." ) );
        //        return new ValueTask();
        //    }, ( reason ) =>
        //    {
        //        tcs.SetResult( reason );
        //    } );
        //    (await tcs.Task).Should().Be( DisconnectReason.RemoteDisconnected );
        //    await packetReplayer.StopAndEnsureValidAsync();
        //} //TODO: enable for client thats doesn't expose PipeReader

        //[Test]
        //public async Task partial_message_throws_with_disposable_application_message_closure()
        //{
        //    TaskCompletionSource<DisconnectReason> tcs = new();

        //    (PacketReplayer packetReplayer, SimpleTestMqtt3Client client) = await Scenario.ConnectedClient( ClassCase, new[]
        //    {
        //        TestPacketHelper.SendToClient("3018000a7465737420746f70696374657374207061796c"),
        //        TestPacketHelper.Disconnect
        //    }, messageProcessor: ( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken token ) =>
        //    {
        //        tcs.TrySetException( new AssertionException( "We shouldn't receive a message in this scenario." ) );
        //        return new ValueTask();
        //    }, ( reason ) =>
        //    {
        //        tcs.SetResult( reason );
        //    } );
        //    (await tcs.Task).Should().Be( DisconnectReason.RemoteDisconnected );
        //    await packetReplayer.StopAndEnsureValidAsync();
        //}//TODO: enable for client thats doesn't expose PipeReader

        [Test]
        public async Task message_with_invalid_size_lead_to_protocol_error()
        {
            TaskCompletionSource<DisconnectReason> tcs = new();

            (PacketReplayer packetReplayer, SimpleTestMqtt3Client client) = await Scenario.ConnectedClient( ClassCase, new[]
            {
                TestPacketHelper.SendToClient("308080808080000a7465737420746f70696374657374207061796c"),
                TestPacketHelper.Disconnect
            }, messageProcessor: ( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken token ) =>
            {
                tcs.TrySetException( new AssertionException( "We shouldn't receive a message in this scenario." ) );
                return new ValueTask();
            }, ( reason ) =>
            {
                tcs.TrySetResult( reason );
            } );
            (await tcs.Task).Should().Be( DisconnectReason.ProtocolError );
            await packetReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task can_parse_5_messages()
        {
            TaskCompletionSource tcs = new();
            int i = 0;
            (PacketReplayer packetReplayer, SimpleTestMqtt3Client client) = await Scenario.ConnectedClient( ClassCase, new[]
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
