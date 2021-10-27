using CK.Core;
using CK.Monitoring;
using CK.MQTT.Client.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;
namespace CK.MQTT.Client.Tests
{
    public class ConnectionTests
    {
        [Test]
        public async Task simple_connection_works()
        {
            PacketReplayer pcktReplayer = new( new[]
            {
                TestPacketHelper.Outgoing("101600044d5154540402001e000a434b4d71747454657374"),
                TestPacketHelper.SendToClient("20020000")
            } );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ), ( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken cancellationToken ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            await pcktReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task connect_with_clean_session_but_connack_session_present_is_not_zero_should_fail()
        {
            PacketReplayer pcktReplayer = new();
            PacketReplayer.TestWorker connectPacket = TestPacketHelper.Outgoing( "101600044d5154540402001e000a434b4d71747454657374" );
            for( byte i = 1; i != 0; i++ ) //Ok there we loop over all non zero bytes.
            {
                pcktReplayer.PacketsWorker.Writer.TryWrite( connectPacket );
                pcktReplayer.PacketsWorker.Writer.TryWrite(
                    TestPacketHelper.SendToClient( "2002" + BitConverter.ToString( new byte[] { i } ) + "00" )
                );
                pcktReplayer.PacketsWorker.Writer.TryWrite( TestPacketHelper.LinkDown );
            }


            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ), ( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken cancellationToken ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            ConnectResult res = await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            res.ConnectError.Should().Be( ConnectError.ProtocolError_SessionNotFlushed );
            for( byte i = 2; i != 0; i++ )
            {
                res = await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
                res.ConnectError.Should().Be( ConnectError.ProtocolError_InvalidConnackState );
            }
            await pcktReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task connect_with_clean_session_but_connack_return_code_is_invalid_should_throw()
        {
            PacketReplayer packetReplayer = new();
            PacketReplayer.TestWorker connectPacket = TestPacketHelper.Outgoing( "101600044d5154540402001e000a434b4d71747454657374" );
            const int startSkipCount = 6; // These packets are valid, so we skip them.
            for( byte i = startSkipCount; i != 0; i++ ) //Ok there we loop over all non zero bytes.
            {
                packetReplayer.PacketsWorker.Writer.TryWrite( connectPacket ).Should().BeTrue();
                packetReplayer.PacketsWorker.Writer.TryWrite(
                    TestPacketHelper.SendToClient( "200200" + BitConverter.ToString( new byte[] { i } ) )
                ).Should().BeTrue();
                packetReplayer.PacketsWorker.Writer.TryWrite( TestPacketHelper.LinkDown );
            }


            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( packetReplayer ), ( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken cancellationToken ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            for( byte i = startSkipCount; i != 0; i++ )
            {
                ConnectResult res = await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
                res.ConnectError.Should().Be( ConnectError.ProtocolError_UnknownReturnCode );
            }
            await packetReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task connect_return_correct_error_code()
        {
            PacketReplayer packetReplayer = new();
            PacketReplayer.TestWorker connectPacket = TestPacketHelper.Outgoing( "101600044d5154540402001e000a434b4d71747454657374" );
            for( byte i = 1; i < 6; i++ ) //Ok there we loop over all non zero bytes.
            {
                packetReplayer.PacketsWorker.Writer.TryWrite( connectPacket ).Should().BeTrue();
                packetReplayer.PacketsWorker.Writer.TryWrite(
                    TestPacketHelper.SendToClient( "200200" + BitConverter.ToString( new byte[] { i } ) )
                ).Should().BeTrue();
                packetReplayer.PacketsWorker.Writer.TryWrite( TestPacketHelper.LinkDown );
            }


            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( packetReplayer ), ( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken cancellationToken ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            for( byte i = 1; i < 6; i++ )
            {
                ConnectResult res = await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
                switch( i )
                {
                    case 1:
                        res.ConnectReturnCode.Should().Be( ConnectReturnCode.UnacceptableProtocolVersion );
                        continue;
                    case 2:
                        res.ConnectReturnCode.Should().Be( ConnectReturnCode.IdentifierRejected );
                        continue;
                    case 3:
                        res.ConnectReturnCode.Should().Be( ConnectReturnCode.ServerUnavailable );
                        continue;
                    case 4:
                        res.ConnectReturnCode.Should().Be( ConnectReturnCode.BadUserNameOrPassword );
                        continue;
                    case 5:
                        res.ConnectReturnCode.Should().Be( ConnectReturnCode.NotAuthorized );
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

            PacketReplayer packetReplayer = new( new[]
            {
                TestPacketHelper.Outgoing( "101600044d5154540402001e000a434b4d71747454657374" )
            } );
            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( packetReplayer ), ( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken cancellationToken ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            Task<ConnectResult> connectTask = client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            connectTask.IsCompleted.Should().BeFalse();
            packetReplayer.TestDelayHandler.IncrementTime( TimeSpan.FromMilliseconds( 4999 ) );
            connectTask.IsCompleted.Should().BeFalse();
            packetReplayer.TestDelayHandler.IncrementTime( TimeSpan.FromMilliseconds( 2 ) );
            (await connectTask).ConnectError.Should().Be( ConnectError.Timeout );
            await packetReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task connect_while_being_connected_should_throw_friendly_exception()
        {
            PacketReplayer packetReplayer = new( new[]
            {
                TestPacketHelper.Outgoing( "101600044d5154540402001e000a434b4d71747454657374" ),
                TestPacketHelper.SendToClient( "20020000" )
            } );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( packetReplayer ), ( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken cancellationToken ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            try
            {
                await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
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
            PacketReplayer.TestWorker connectPacket = TestPacketHelper.Outgoing( "101600044d5154540402001e000a434b4d71747454657374" );
            PacketReplayer packetReplayer = new( new[]
            {
                connectPacket,
                TestPacketHelper.SendToClient( "20021000" ), // Invalid response.
                TestPacketHelper.LinkDown,
                connectPacket,
                TestPacketHelper.SendToClient( "20020000" ) // Valid response.
            } );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( packetReplayer ), ( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken cancellationToken ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            ConnectResult res = await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            res.ConnectError.Should().NotBe( ConnectError.Ok );
            ConnectResult res2 = await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            res2.ConnectError.Should().Be( ConnectError.Ok );
            await packetReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task oversized_connack_is_parsed()
        {
            PacketReplayer pcktReplayer = new( new[]
            {
                TestPacketHelper.Outgoing("101600044d5154540402001e000a434b4d71747454657374"),
                TestPacketHelper.SendToClient("2003000000"),
                TestPacketHelper.SendToClient("321a000a7465737420746f706963000174657374207061796c6f6164")
            } );
            TaskCompletionSource tcs = new();
            ApplicationMessage? msg = null;
            InputMonitorCounter inputLogger = new( new InputLoggerMqttActivityMonitor( new ActivityMonitor( "Input Logger" ) ) );
            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer, inputLogger: inputLogger ),
                ( IActivityMonitor? m, ApplicationMessage incMsg, CancellationToken cancellationToken ) =>
            {
                msg = incMsg;
                tcs.SetResult();
                return new ValueTask();
            } );
            await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
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
            PacketReplayer pcktReplayer = new( new[]
            {
                TestPacketHelper.Outgoing("101600044d5154540402001e000a434b4d71747454657374"),
                TestPacketHelper.SendToClient("20020000")
            } );

            InputMonitorCounter inputLogger = new( new InputLoggerMqttActivityMonitor( new ActivityMonitor( "Input Logger" ) ) );
            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer, inputLogger: inputLogger ),
                ( IActivityMonitor? m, DisposableApplicationMessage msg, CancellationToken cancellationToken ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            inputLogger.UnparsedExtraBytesCounter.Should().Be( 0 );
            await pcktReplayer.StopAndEnsureValidAsync();
        }
    }
}
