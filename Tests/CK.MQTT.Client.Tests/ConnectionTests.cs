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
        public async Task simple_connection_works()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );

            var task = client.ConnectAsync();

            await replayer.AssertClientSent( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
            await replayer.SendToClient( TestHelper.Monitor, "20020000" );

            var result = await task;
            result.ConnectReturnCode.Should().Be( ConnectReturnCode.Accepted );
            replayer.Events.Reader.Count.Should().Be( 0 );
        }

        [Test]
        public async Task connect_with_clean_session_but_connack_session_present_is_not_zero_should_fail()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );

            for( byte i = 1; i != 0; i++ ) //Ok there we loop over all non zero bytes.
            {
                var task = client.ConnectAsync();
                await replayer.AssertClientSent( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
                await replayer.SendToClient( TestHelper.Monitor, "2002" + BitConverter.ToString( new byte[] { i } ) + "00" );
                var res = await task;
                if( i == 1 )
                {
                    res.Should().Be( new ConnectResult( ConnectError.ProtocolError_SessionNotFlushed ) );
                }
                else
                {
                    res.Should().Be( new ConnectResult( ConnectError.ProtocolError_InvalidConnackState ) );
                }
            }
            replayer.Events.Reader.Count.Should().Be( 0 );
        }



        [Test]
        public async Task connect_with_clean_session_but_connack_return_code_is_invalid_should_throw()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );

            const int startSkipCount = 6; // These packets are valid, so we skip them.
            for( byte i = startSkipCount; i != 0; i++ ) //Ok there we loop over all non zero bytes.
            {
                var task = client.ConnectAsync();

                await replayer.AssertClientSent( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
                await replayer.SendToClient( TestHelper.Monitor, "200200" + BitConverter.ToString( new byte[] { i } ) );
                var res = await task;
                res.Should().Be( new ConnectResult( ConnectError.ProtocolError_UnknownReturnCode ) );
            }
            replayer.Events.Reader.Count.Should().Be( 0 );
        }

        [Test]
        public async Task connect_return_correct_error_code()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            for( byte i = 1; i < 6; i++ )
            {
                var task = client.ConnectAsync();

                await replayer.AssertClientSent( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
                await replayer.SendToClient( TestHelper.Monitor, "200200" + BitConverter.ToString( new byte[] { i } ) );
                var res = await task;
                switch( i )
                {
                    case 1:
                        res.Should().Be( new ConnectResult( SessionState.CleanSession, ConnectReturnCode.UnacceptableProtocolVersion ) );
                        continue;
                    case 2:
                        res.Should().Be( new ConnectResult( SessionState.CleanSession, ConnectReturnCode.IdentifierRejected ) );
                        continue;
                    case 3:
                        res.Should().Be( new ConnectResult( SessionState.CleanSession, ConnectReturnCode.ServerUnavailable ) );
                        continue;
                    case 4:
                        res.Should().Be( new ConnectResult( SessionState.CleanSession, ConnectReturnCode.BadUserNameOrPassword ) );
                        continue;
                    case 5:
                        res.Should().Be( new ConnectResult( SessionState.CleanSession, ConnectReturnCode.NotAuthorized ) );
                        continue;
                    default:
                        Assert.Fail();
                        return;
                }
            }
            replayer.Events.Reader.Count.Should().Be( 0 );
        }

        [Test]
        public async Task connect_throw_timeout()
        {

            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            var connectTask = client.ConnectAsync();
            await replayer.AssertClientSent( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
            connectTask.IsCompleted.Should().BeFalse();
            await Task.Delay( 1 );
            replayer.TestDelayHandler.IncrementTime( TimeSpan.FromMilliseconds( 4999 ) );
            await Task.Delay( 1 );
            connectTask.IsCompleted.Should().BeFalse();
            replayer.TestDelayHandler.IncrementTime( TimeSpan.FromMilliseconds( 2 ) );
            (await connectTask).ConnectError.Should().Be( ConnectError.Timeout );
            replayer.Events.Reader.Count.Should().Be( 0 );
        }

        [Test]
        public async Task connect_while_being_connected_should_throw_friendly_exception()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );

            var task = client.ConnectAsync();

            await replayer.AssertClientSent( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
            await replayer.SendToClient( TestHelper.Monitor, "20020000" );

            await task;
            try
            {
                await client.ConnectAsync();
                Assert.Fail();
            }
            catch( Exception e )
            {
                e.Should().BeOfType<InvalidOperationException>();
                e.Message.Should().Be( "This client is already connected." );
            }
            replayer.Events.Reader.Count.Should().Be( 0 );
        }

        [Test]
        public async Task connect_after_failed_connect_works()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );

            var task = client.ConnectAsync();

            await replayer.AssertClientSent( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
            await replayer.SendToClient( TestHelper.Monitor, "20021000" );
            var res = await task;
            res.ConnectError.Should().NotBe( ConnectError.None );
            var task2 = client.ConnectAsync();

            await replayer.AssertClientSent( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
            await replayer.SendToClient( TestHelper.Monitor, "20020000" );
            var res2 = await task2;
            res2.ConnectError.Should().Be( ConnectError.None );
            replayer.Events.Reader.Count.Should().Be( 0 );
        }

        [TestCase( 8000u )]
        [TestCase( 3u )]
        public async Task oversized_connack_is_parsed( uint connackSize )
        {
            uint size = 1 + connackSize.CompactByteCount() + connackSize;
            Memory<byte> connackBuffer = new byte[size];
            connackBuffer.Span[0] = 0x20;
            connackBuffer.Span[1..].WriteVariableByteInteger( connackSize );


            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            var task = client.ConnectAsync();
            await replayer.AssertClientSent( TestHelper.Monitor, "101600044d51545404020000000a434b4d71747454657374" );
            await replayer.SendToClient( TestHelper.Monitor, connackBuffer );

            await replayer.SendToClient( TestHelper.Monitor, "321a000a7465737420746f706963000174657374207061796c6f6164" );
            var msg = await replayer.ShouldContainEventAsync<ApplicationMessage>();
            msg.Should().NotBeNull();
            msg.Should().BeEquivalentTo( new ApplicationMessage(
                "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.AtLeastOnce, false )
            );
            await replayer.ShouldContainEventAsync<TestMqttClient.UnparsedExtraData>();
            replayer.Events.Reader.Count.Should().Be( 0 );
        }


        [Test]
        public async Task anonymous_connect_works()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer, credentials: new MqttClientCredentials() ) );

            var task = client.ConnectAsync();

            await replayer.AssertClientSent( TestHelper.Monitor, "100C00044D515454040200000000" );
            await replayer.SendToClient( TestHelper.Monitor, "20020000" );

            var result = await task;
            result.ConnectReturnCode.Should().Be( ConnectReturnCode.Accepted );
            replayer.Events.Reader.Count.Should().Be( 0 );
        }
    }
}
