using CK.MQTT.Client.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;
namespace CK.MQTT.Client.Tests
{
    public class PublishTests
    {
        [Test]
        public async Task simple_publish_qos0_works()
        {
            (PacketReplayer packetReplayer, IMqtt3Client client) = await Scenario.ConnectedClient( new List<TestPacket>()
            {
                TestPacket.Outgoing("3018000a7465737420746f70696374657374207061796c6f6164")
            } );

            await await client.PublishAsync( TestHelper.Monitor, new ApplicationMessage(
                "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.AtMostOnce, false )
            );
            packetReplayer.LastWorkTask!.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Test]
        public async Task simple_publish_qos1_works()
        {
            (PacketReplayer packetReplayer, IMqtt3Client client) = await Scenario.ConnectedClient( new List<TestPacket>()
            {
                TestPacket.Outgoing("321a000a7465737420746f706963000174657374207061796c6f6164"),
                TestPacket.Incoming("40020001")
            } );

            await await client.PublishAsync( TestHelper.Monitor, new ApplicationMessage(
                "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.AtLeastOnce, false )
            );
            packetReplayer.LastWorkTask!.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Test]
        public async Task simple_publish_qos2_works()
        {
            (PacketReplayer packetReplayer, IMqtt3Client client) = await Scenario.ConnectedClient( new List<TestPacket>()
            {
                TestPacket.Outgoing("341a000a7465737420746f706963000174657374207061796c6f6164"),
                TestPacket.Incoming("50020001"),
                TestPacket.Outgoing("62020001"),
                TestPacket.Incoming("70020001")
            } );

            await await client.PublishAsync( TestHelper.Monitor, new ApplicationMessage(
                "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.ExactlyOnce, false )
            );
            packetReplayer.LastWorkTask!.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Test]
        public async Task todo()
        {
            throw new NotImplementedException();
            // test the case where a bad packet block a packet (cycling the ID don't reuse the blocked packet ID.)
        }
    }
}
