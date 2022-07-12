using CK.Core;
using CK.MQTT.Client.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests
{
    public class ConnectionTests_PipeReaderCop : ConnectionTests
    {
        public override string ClassCase => "PipeReaderCop";
    }

    public class ConnectionTests_Default : ConnectionTests
    {
        public override string ClassCase => "Default";
    }

    public class ConnectionTests_BytePerByteChannel : ConnectionTests
    {
        public override string ClassCase => "BytePerByte";
    }

    public abstract class ConnectionTests
    {
        public abstract string ClassCase { get; }

        [Test]
        public async Task simple_connection_works_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );

            var task = client.ConnectAsync();

            await replayer.AssertClientSentAsync( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
            await replayer.SendToClientAsync( TestHelper.Monitor, "20020000" );
            await replayer.ShouldContainEventAsync<LoopBackBase.StartedChannel>();
            await replayer.ShouldContainEventAsync<DefaultClientMessageSink.Connected>();

            var result = await task;
            result.ProtocolReturnCode.Should().Be( ProtocolConnectReturnCode.Accepted );
            replayer.Events.Reader.Count.Should().Be( 0 );
        }

        [Test]
        public async Task connect_with_clean_session_but_connack_session_present_is_not_zero_should_fail_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );

            for( byte i = 1; i != 0; i++ ) //Ok there we loop over all non zero bytes.
            {
                var task = client.ConnectAsync();

                await replayer.AssertClientSentAsync( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
                await replayer.SendToClientAsync( TestHelper.Monitor, "2002" + BitConverter.ToString( new byte[] { i } ) + "00" );
                var res = await task;
                if( i == 1 )
                {
                    res.Should().Be( new ConnectResult( ConnectError.ProtocolError_SessionNotFlushed ) );
                }
                else
                {
                    res.Should().Be( new ConnectResult( ConnectError.ProtocolError_InvalidConnackState ) );
                }
                await replayer.ShouldContainEventAsync<LoopBackBase.StartedChannel>();
                await replayer.ShouldContainEventAsync<LoopBackBase.ClosedChannel>();
                await replayer.ShouldContainEventAsync<DefaultClientMessageSink.FailedManualConnect>();
            }
            replayer.Events.Reader.Count.Should().Be( 0 );
        }



        [Test]
        public async Task connect_with_clean_session_but_connack_return_code_is_invalid_should_throw_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );

            const int startSkipCount = 6; // These packets are valid, so we skip them.
            for( byte i = startSkipCount; i != 0; i++ ) //Ok there we loop over all non zero bytes.
            {
                var task = client.ConnectAsync();

                await replayer.AssertClientSentAsync( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
                await replayer.SendToClientAsync( TestHelper.Monitor, "200200" + BitConverter.ToString( new byte[] { i } ) );
                var res = await task;
                res.Should().Be( new ConnectResult( ConnectError.ProtocolError_UnknownReturnCode ) );
                await replayer.ShouldContainEventAsync<LoopBackBase.StartedChannel>();
                await replayer.ShouldContainEventAsync<LoopBackBase.ClosedChannel>();
                await replayer.ShouldContainEventAsync<DefaultClientMessageSink.FailedManualConnect>();
            }
            replayer.Events.Reader.Count.Should().Be( 0 );
        }

        [Test]
        public async Task connect_return_correct_error_code_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            for( byte i = 1; i < 6; i++ )
            {
                var task = client.ConnectAsync();

                await replayer.AssertClientSentAsync( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
                await replayer.SendToClientAsync( TestHelper.Monitor, "200200" + BitConverter.ToString( new byte[] { i } ) );
                var res = await task;
                switch( i )
                {
                    case 1:
                        res.Should().Be( new ConnectResult( SessionState.CleanSession, ProtocolConnectReturnCode.UnacceptableProtocolVersion ) );
                        break;
                    case 2:
                        res.Should().Be( new ConnectResult( SessionState.CleanSession, ProtocolConnectReturnCode.IdentifierRejected ) );
                        break;
                    case 3:
                        res.Should().Be( new ConnectResult( SessionState.CleanSession, ProtocolConnectReturnCode.ServerUnavailable ) );
                        break;
                    case 4:
                        res.Should().Be( new ConnectResult( SessionState.CleanSession, ProtocolConnectReturnCode.BadUserNameOrPassword ) );
                        break;
                    case 5:
                        res.Should().Be( new ConnectResult( SessionState.CleanSession, ProtocolConnectReturnCode.NotAuthorized ) );
                        break;
                    default:
                        Assert.Fail();
                        return;
                }
                await replayer.ShouldContainEventAsync<LoopBackBase.StartedChannel>();
                await replayer.ShouldContainEventAsync<LoopBackBase.ClosedChannel>();
                await replayer.ShouldContainEventAsync<DefaultClientMessageSink.FailedManualConnect>();
            }
            replayer.Events.Reader.Count.Should().Be( 0 );
        }

        [Test]
        public async Task connect_throw_timeout_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            var connectTask = client.ConnectAsync();
            await replayer.AssertClientSentAsync( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
            connectTask.IsCompleted.Should().BeFalse();
            await Task.Delay( 1 );
            replayer.TestTimeHandler.IncrementTime( TimeSpan.FromMilliseconds( 4999 ) );
            await Task.Delay( 1 );
            connectTask.IsCompleted.Should().BeFalse();
            replayer.TestTimeHandler.IncrementTime( TimeSpan.FromMilliseconds( 2 ) );
            (await connectTask).Error.Should().Be( ConnectError.Timeout );
            await replayer.ShouldContainEventAsync<LoopBackBase.StartedChannel>();
            await replayer.ShouldContainEventAsync<LoopBackBase.ClosedChannel>();
            await replayer.ShouldContainEventAsync<DefaultClientMessageSink.FailedManualConnect>();
            replayer.Events.Reader.Count.Should().Be( 0 );
        }

        [Test]
        public async Task connect_while_being_connected_should_throw_friendly_exception_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );

            var task = client.ConnectAsync();

            await replayer.AssertClientSentAsync( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
            await replayer.SendToClientAsync( TestHelper.Monitor, "20020000" );
            await replayer.ShouldContainEventAsync<LoopBackBase.StartedChannel>();
            await replayer.ShouldContainEventAsync<DefaultClientMessageSink.Connected>();

            await task;
            try
            {
                await client.ConnectAsync();
                Assert.Fail();
            }
            catch( Exception e )
            {
                e.Should().BeOfType<InvalidOperationException>();
            }
            replayer.Events.Reader.Count.Should().Be( 0 );
        }

        [Test]
        public async Task connect_after_failed_connect_works_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );

            var task = client.ConnectAsync();

            await replayer.AssertClientSentAsync( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
            await replayer.SendToClientAsync( TestHelper.Monitor, "20021000" );
            await replayer.ShouldContainEventAsync<LoopBackBase.StartedChannel>();

            var res = await task;
            res.Error.Should().NotBe( ConnectError.None );
            await replayer.ShouldContainEventAsync<LoopBackBase.ClosedChannel>();
            await replayer.ShouldContainEventAsync<DefaultClientMessageSink.FailedManualConnect>();

            var task2 = client.ConnectAsync();

            await replayer.AssertClientSentAsync( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
            await replayer.SendToClientAsync( TestHelper.Monitor, "20020000" );
            await replayer.ShouldContainEventAsync<LoopBackBase.StartedChannel>();
            await replayer.ShouldContainEventAsync<DefaultClientMessageSink.Connected>();
            var res2 = await task2;
            res2.Error.Should().Be( ConnectError.None );
            replayer.Events.Reader.Count.Should().Be( 0 );
        }

        [TestCase( 8000u )]
        [TestCase( 3u )]
        public async Task oversized_connack_is_parsed_Async( uint connackSize )
        {
            uint size = 1 + connackSize.CompactByteCount() + connackSize;
            Memory<byte> connackBuffer = new byte[size];
            connackBuffer.Span[0] = 0x20;
            connackBuffer.Span[1..].WriteVariableByteInteger( connackSize );


            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            var task = client.ConnectAsync();
            await replayer.AssertClientSentAsync( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
            await replayer.SendToClientAsync( TestHelper.Monitor, connackBuffer );
            await replayer.ShouldContainEventAsync<LoopBackBase.StartedChannel>();
            await replayer.ShouldContainEventAsync<MqttMessageSink.UnparsedExtraData>();
            await replayer.ShouldContainEventAsync<DefaultClientMessageSink.Connected>();
            var res = await task;
            res.Status.Should().Be( ConnectStatus.Successful );
            await replayer.SendToClientAsync( TestHelper.Monitor, "321a000a7465737420746f706963000174657374207061796c6f6164" );
            var msg = await replayer.ShouldContainEventAsync<VolatileApplicationMessage>();
            msg.Should().NotBeNull();
            msg.Should().BeEquivalentTo( new VolatileApplicationMessage( new ApplicationMessage(
                "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.AtLeastOnce, false )
            , new DisposableComposite() ) );
            replayer.Events.Reader.Count.Should().Be( 0 );
        }


        [Test]
        public async Task anonymous_connect_works_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer, credentials: new MqttClientCredentials() ) );

            var task = client.ConnectAsync();

            await replayer.AssertClientSentAsync( TestHelper.Monitor, "100C00044D515454040200000000" );
            await replayer.SendToClientAsync( TestHelper.Monitor, "20020000" );

            var result = await task;
            result.ProtocolReturnCode.Should().Be( ProtocolConnectReturnCode.Accepted );
            await replayer.ShouldContainEventAsync<LoopBackBase.StartedChannel>();
            await replayer.ShouldContainEventAsync<DefaultClientMessageSink.Connected>();
            replayer.Events.Reader.Count.Should().Be( 0 );
        }

        [Test]
        public async Task invalid_length_connack_lead_to_end_of_stream_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer, credentials: new MqttClientCredentials() ) );

            var task = client.ConnectAsync();

            await replayer.AssertClientSentAsync( TestHelper.Monitor, "100C00044D515454040200000000" );
            await replayer.SendToClientAsync( TestHelper.Monitor, "20030000" );
            replayer.Channel!.CloseConnectionBackdoor();

            var result = await task;
            result.Should().Be( new ConnectResult( ConnectError.ProtocolError_IncompleteResponse ) );

        }
    }
}
