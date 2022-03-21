using CK.Core;
using CK.MQTT.Client.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests
{
    [ExcludeFromCodeCoverage]
    public class ConnectionTests_PipeReaderCop : ConnectionTests
    {
        public override string ClassCase => "PipeReaderCop";
    }

    [ExcludeFromCodeCoverage]
    public class ConnectionTests_Default : ConnectionTests
    {
        public override string ClassCase => "Default";
    }

    [ExcludeFromCodeCoverage]
    public class ConnectionTests_BytePerByteChannel : ConnectionTests
    {
        public override string ClassCase => "BytePerByte";
    }

    [ExcludeFromCodeCoverage]
    public abstract class ConnectionTests
    {
        public abstract string ClassCase { get; }

        [Test]
        public async Task simple_connection_works()
        {
            PacketReplayer pcktReplayer = new( ClassCase, new[]
            {
                TestPacketHelper.Outgoing("101600044d51545404020000000a434b4d71747454657374"),
                TestPacketHelper.SendToClient("20020000")
            } );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ), ClientHelpers.NotListening_Dispose );
            await client.ConnectAsync( TestHelper.Monitor );
            await pcktReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task connect_with_clean_session_but_connack_session_present_is_not_zero_should_fail()
        {
            PacketReplayer pcktReplayer = new( ClassCase );
            PacketReplayer.TestWorker connectPacket = TestPacketHelper.Outgoing( "101600044d51545404020000000a434b4d71747454657374" );
            for( byte i = 1; i != 0; i++ ) //Ok there we loop over all non zero bytes.
            {
                pcktReplayer.PacketsWorker.Writer.TryWrite( connectPacket );
                pcktReplayer.PacketsWorker.Writer.TryWrite(
                    TestPacketHelper.SendToClient( "2002" + BitConverter.ToString( new byte[] { i } ) + "00" )
                );
                pcktReplayer.PacketsWorker.Writer.TryWrite( TestPacketHelper.WaitClientDisconnect );
            }


            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ), ClientHelpers.NotListening_Dispose );
            ConnectResult res = await client.ConnectAsync( TestHelper.Monitor );
            res.Should().Be( new ConnectResult( ConnectError.ProtocolError_SessionNotFlushed ) );

            for( byte i = 2; i != 0; i++ )
            {
                res = await client.ConnectAsync( TestHelper.Monitor );
                res.Should().Be( new ConnectResult( ConnectError.ProtocolError_InvalidConnackState ) );
            }
            await pcktReplayer.StopAndEnsureValidAsync();
        }

        

        [Test]
        public async Task connect_with_clean_session_but_connack_return_code_is_invalid_should_throw()
        {
            PacketReplayer packetReplayer = new( ClassCase );
            PacketReplayer.TestWorker connectPacket = TestPacketHelper.Outgoing( "101600044d51545404020000000a434b4d71747454657374" );
            const int startSkipCount = 6; // These packets are valid, so we skip them.
            for( byte i = startSkipCount; i != 0; i++ ) //Ok there we loop over all non zero bytes.
            {
                packetReplayer.PacketsWorker.Writer.TryWrite( connectPacket ).Should().BeTrue();
                packetReplayer.PacketsWorker.Writer.TryWrite(
                    TestPacketHelper.SendToClient( "200200" + BitConverter.ToString( new byte[] { i } ) )
                ).Should().BeTrue();
                packetReplayer.PacketsWorker.Writer.TryWrite( TestPacketHelper.WaitClientDisconnect );
            }


            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( packetReplayer ), ClientHelpers.NotListening_Dispose );
            for( byte i = startSkipCount; i != 0; i++ )
            {
                ConnectResult res = await client.ConnectAsync( TestHelper.Monitor );
                res.Should().Be( new ConnectResult( ConnectError.ProtocolError_UnknownReturnCode ) );
            }
            await packetReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task connect_return_correct_error_code()
        {
            PacketReplayer packetReplayer = new( ClassCase );
            PacketReplayer.TestWorker connectPacket = TestPacketHelper.Outgoing( "101600044d51545404020000000a434b4d71747454657374" );
            for( byte i = 1; i < 6; i++ ) //Ok there we loop over all non zero bytes.
            {
                packetReplayer.PacketsWorker.Writer.TryWrite( connectPacket ).Should().BeTrue();
                packetReplayer.PacketsWorker.Writer.TryWrite(
                    TestPacketHelper.SendToClient( "200200" + BitConverter.ToString( new byte[] { i } ) )
                ).Should().BeTrue();
                packetReplayer.PacketsWorker.Writer.TryWrite( TestPacketHelper.WaitClientDisconnect );
            }


            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( packetReplayer ), ClientHelpers.NotListening_Dispose );
            for( byte i = 1; i < 6; i++ )
            {
                ConnectResult res = await client.ConnectAsync( TestHelper.Monitor );
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
            await packetReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task connect_throw_timeout()
        {

            PacketReplayer packetReplayer = new( ClassCase, new[]
            {
                TestPacketHelper.Outgoing( "101600044d51545404020000000a434b4d71747454657374" )
            } );
            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( packetReplayer ), ClientHelpers.NotListening_Dispose );
            Task<ConnectResult> connectTask = client.ConnectAsync( TestHelper.Monitor );
            connectTask.IsCompleted.Should().BeFalse();
            await Task.Delay( 1 );
            packetReplayer.TestDelayHandler.IncrementTime( TimeSpan.FromMilliseconds( 4999 ) );
            await Task.Delay( 1 );
            connectTask.IsCompleted.Should().BeFalse();
            packetReplayer.TestDelayHandler.IncrementTime( TimeSpan.FromMilliseconds( 2 ) );
            (await connectTask).ConnectError.Should().Be( ConnectError.Timeout );
            await packetReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task connect_while_being_connected_should_throw_friendly_exception()
        {
            PacketReplayer packetReplayer = new( ClassCase, new[]
            {
                TestPacketHelper.Outgoing( "101600044d51545404020000000a434b4d71747454657374" ),
                TestPacketHelper.SendToClient( "20020000" )
            } );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( packetReplayer ), ClientHelpers.NotListening_Dispose );
            await client.ConnectAsync( TestHelper.Monitor );
            try
            {
                await client.ConnectAsync( TestHelper.Monitor );
                Assert.Fail();
            }
            catch( Exception e )
            {
                e.Should().BeOfType<InvalidOperationException>();
                e.Message.Should().Be( "This client is already connected." );
            }
            await packetReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task connect_after_failed_connect_works()
        {
            PacketReplayer.TestWorker connectPacket = TestPacketHelper.Outgoing( "101600044d51545404020000000a434b4d71747454657374" );
            PacketReplayer packetReplayer = new( ClassCase, new[]
            {
                connectPacket,
                TestPacketHelper.SendToClient( "20021000" ), // Invalid response.
                TestPacketHelper.WaitClientDisconnect,
                connectPacket,
                TestPacketHelper.SendToClient( "20020000" ) // Valid response.
            } );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( packetReplayer ), ClientHelpers.NotListening_Dispose );
            ConnectResult res = await client.ConnectAsync( TestHelper.Monitor );
            res.ConnectError.Should().NotBe( ConnectError.None );
            ConnectResult res2 = await client.ConnectAsync( TestHelper.Monitor );
            res2.ConnectError.Should().Be( ConnectError.None );
            await packetReplayer.StopAndEnsureValidAsync();
        }

        [TestCase( 8000u )]
        [TestCase( 3u )]
        public async Task oversized_connack_is_parsed( uint connackSize )
        {
            uint size = 1 + connackSize.CompactByteCount() + connackSize;
            Memory<byte> connackBuffer = new byte[size];
            connackBuffer.Span[0] = 0x20;
            connackBuffer.Span[1..].WriteVariableByteInteger( connackSize );
            PacketReplayer pcktReplayer = new( ClassCase, new[]
            {
                TestPacketHelper.Outgoing("101600044d51545404020000000a434b4d71747454657374"),
                TestPacketHelper.SendToClient(connackBuffer),
                TestPacketHelper.SendToClient("321a000a7465737420746f706963000174657374207061796c6f6164")
            } );
            TaskCompletionSource tcs = new();
            ApplicationMessage? msg = null;
            InputMonitorCounter inputLogger = new( new InputLoggerMqttActivityMonitor( new ActivityMonitor( "Input Logger" ) ) );
            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer, inputLogger: inputLogger ),
                ClientHelpers.NotListening_Dispose );
            await client.ConnectAsync( TestHelper.Monitor );
            await tcs.Task;
            msg.Should().NotBeNull();
            msg.Should().BeEquivalentTo( new ApplicationMessage(
                "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.AtLeastOnce, false )
            );
            inputLogger.UnparsedExtraBytesCounter.Should().Be( 1 );
            await pcktReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task connect_ack_is_parsed_without_skipping_data()
        {
            PacketReplayer pcktReplayer = new( ClassCase, new[]
            {
                TestPacketHelper.Outgoing("101600044d51545404020000000a434b4d71747454657374"),
                TestPacketHelper.SendToClient("20020000")
            } );

            InputMonitorCounter inputLogger = new( new InputLoggerMqttActivityMonitor( new ActivityMonitor( "Input Logger" ) ) );
            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer, inputLogger: inputLogger ),
                ClientHelpers.NotListening_Dispose );
            await client.ConnectAsync( TestHelper.Monitor );
            inputLogger.UnparsedExtraBytesCounter.Should().Be( 0 );
            await pcktReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task anonymous_connect()
        {
            PacketReplayer pcktReplayer = new( ClassCase, new[]
            {
                TestPacketHelper.Outgoing("100C00044D515454040200000000"),
                TestPacketHelper.SendToClient("20020000")
            } );

            InputMonitorCounter inputLogger = new( new InputLoggerMqttActivityMonitor( new ActivityMonitor( "Input Logger" ) ) );
            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client(
                TestConfigs.DefaultTestConfig( pcktReplayer, inputLogger: inputLogger, credentials: new MqttClientCredentials() ),
                 ClientHelpers.NotListening_Dispose
            );
            await client.ConnectAsync( TestHelper.Monitor );
            inputLogger.UnparsedExtraBytesCounter.Should().Be( 0 );
            await pcktReplayer.StopAndEnsureValidAsync();
        }
    }
}
