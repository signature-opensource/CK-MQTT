using CK.Core;
using CK.Monitoring;
using CK.MQTT.Client.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;
namespace CK.MQTT.Client.Tests
{
    public class Tests
    {
        [Test]
        public async Task simple_connection_works()
        {
            PacketReplayer pcktReplayer = new( new Queue<TestPacket>( new List<TestPacket>()
            {
                TestPacket.Outgoing("101600044d5154540402001e000a434b4d71747454657374"),
                TestPacket.Incoming("20020000")
            } ) );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ), ( IActivityMonitor m, DisposableApplicationMessage msg ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            pcktReplayer.LastWorkTask!.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Test]
        public async Task connect_with_clean_session_but_connack_session_present_is_not_zero_should_fail()
        {
            Queue<TestPacket> packets = new();
            TestPacket connectPacket = TestPacket.Outgoing( "101600044d5154540402001e000a434b4d71747454657374" );
            for( byte i = 1; i != 0; i++ ) //Ok there we loop over all non zero bytes.
            {
                packets.Enqueue( connectPacket );
                packets.Enqueue( TestPacket.Incoming( "2002" + BitConverter.ToString( new byte[] { i } ) + "00" ) );
            }

            PacketReplayer pcktReplayer = new( packets );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ), ( IActivityMonitor m, DisposableApplicationMessage msg ) =>
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
            pcktReplayer.LastWorkTask!.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Test]
        public async Task connect_with_clean_session_but_connack_return_code_is_invalid_should_throw()
        {
            Queue<TestPacket> packets = new();
            TestPacket connectPacket = TestPacket.Outgoing( "101600044d5154540402001e000a434b4d71747454657374" );
            const int startSkipCount = 6; // These packets are valid, so we skip them.
            for( byte i = startSkipCount; i != 0; i++ ) //Ok there we loop over all non zero bytes.
            {
                packets.Enqueue( connectPacket );
                packets.Enqueue( TestPacket.Incoming( "200200" + BitConverter.ToString( new byte[] { i } ) ) );
            }

            PacketReplayer packetReplayer = new( packets );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( packetReplayer ), ( IActivityMonitor m, DisposableApplicationMessage msg ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            for( byte i = startSkipCount; i != 0; i++ )
            {
                ConnectResult res = await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
                res.ConnectError.Should().Be( ConnectError.ProtocolError_UnknownReturnCode );
            }
            packetReplayer.LastWorkTask!.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Test]
        public async Task connect_throw_timeout()
        {
            Queue<TestPacket> packets = new();
            packets.Enqueue( TestPacket.Outgoing( "101600044d5154540402001e000a434b4d71747454657374" ) );
            PacketReplayer packetReplayer = new( packets );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( packetReplayer ), ( IActivityMonitor m, DisposableApplicationMessage msg ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            Task<ConnectResult> connectTask = client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            connectTask.IsCompleted.Should().BeFalse();
            await packetReplayer.LastWorkTask!;
            packetReplayer.TestDelayHandler.IncrementTime( TimeSpan.FromMilliseconds( 4999 ) );
            await Task.WhenAny( connectTask, Task.Delay( 50 ) );
            connectTask.IsCompleted.Should().BeFalse();
            packetReplayer.TestDelayHandler.IncrementTime( TimeSpan.FromMilliseconds( 2 ) );
            (await connectTask).ConnectError.Should().Be( ConnectError.Timeout );
            packetReplayer.LastWorkTask!.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Test]
        public async Task connect_while_being_connected_should_throw_friendly_exception()
        {
            Queue<TestPacket> packets = new();
            packets.Enqueue( TestPacket.Outgoing( "101600044d5154540402001e000a434b4d71747454657374" ) );
            packets.Enqueue( TestPacket.Incoming( "20020000" ) );
            PacketReplayer packetReplayer = new( packets );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( packetReplayer ), ( IActivityMonitor m, DisposableApplicationMessage msg ) =>
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
            packetReplayer.LastWorkTask!.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Test]
        public async Task connect_after_failed_connect_works()
        {
            TestPacket connectPacket = TestPacket.Outgoing( "101600044d5154540402001e000a434b4d71747454657374" );
            Queue<TestPacket> packets = new();
            packets.Enqueue( connectPacket );
            packets.Enqueue( TestPacket.Incoming( "20021000" ) ); // Invalid response.
            packets.Enqueue( connectPacket );
            packets.Enqueue( TestPacket.Incoming( "20020000" ) ); // Valid response.
            PacketReplayer packetReplayer = new( packets );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( packetReplayer ), ( IActivityMonitor m, DisposableApplicationMessage msg ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            ConnectResult res = await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            res.ConnectError.Should().NotBe( ConnectError.Ok );
            ConnectResult res2 = await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            res2.ConnectError.Should().Be( ConnectError.Ok );
            packetReplayer.LastWorkTask!.IsCompletedSuccessfully.Should().BeTrue();
        }
    }
}
