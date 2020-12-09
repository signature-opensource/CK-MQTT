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

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ), ( DisposableApplicationMessage msg ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
            pcktReplayer.LastWorkTask!.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Test]
        public async Task connect_with_clean_session_but_connack_session_present_is_not_zero_should_throw()
        {
            Queue<TestPacket> packets = new();
            TestPacket connectPacket = TestPacket.Outgoing( "101600044d5154540402001e000a434b4d71747454657374" );
            for( byte i = 1; i != 0; i++ ) //Ok there we loop over all non zero bytes.
            {
                packets.Enqueue( connectPacket );
                packets.Enqueue( TestPacket.Incoming( "2002" + BitConverter.ToString( new byte[] { i } ) + "00" ) );
            }

            PacketReplayer pcktReplayer = new( packets );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ), ( DisposableApplicationMessage msg ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            for( byte i = 1; i != 0; i++ )
            {
                try
                {
                    ConnectResult res = await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
                    Assert.Fail();
                }
                catch( Exception e )
                {
                    e.Should().BeOfType<ProtocolViolationException>();
                }
            }
            pcktReplayer.LastWorkTask!.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Test]
        public async Task connect_with_clean_session_but_connack_return_code_is_invalid_should_throw()
        {
            Queue<TestPacket> packets = new();
            TestPacket connectPacket = TestPacket.Outgoing( "101600044d5154540402001e000a434b4d71747454657374" );
            for( byte i = 6; i != 0; i++ ) //Ok there we loop over all non zero bytes.
            {
                packets.Enqueue( connectPacket );
                packets.Enqueue( TestPacket.Incoming( "200200" + BitConverter.ToString( new byte[] { i } ) ) );
            }

            PacketReplayer pcktReplayer = new( packets );

            IMqtt3Client client = MqttClient.Factory.CreateMQTT3Client( TestConfigs.DefaultTestConfig( pcktReplayer ), ( DisposableApplicationMessage msg ) =>
            {
                msg.Dispose();
                return new ValueTask();
            } );
            for( byte i = 1; i != 0; i++ )
            {
                try
                {
                    ConnectResult res = await client.ConnectAsync( TestHelper.Monitor, new MqttClientCredentials( "CKMqttTest", true ) );
                    Assert.Fail();
                }
                catch( Exception e )
                {
                    e.Should().BeOfType<ProtocolViolationException>();
                }
            }
            pcktReplayer.LastWorkTask!.IsCompletedSuccessfully.Should().BeTrue();
        }
    }
}
