using CK.MQTT.Client.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests
{
    public class PublishTests_Default : PublishTests
    {
        public override string ClassCase => "Default";
    }

    public class PublishTests_BytePerByteChannel : PublishTests
    {
        public override string ClassCase => "BytePerByte";
    }

    public abstract class PublishTests
    {
        public abstract string ClassCase { get; }

        [Test]
        public async Task simple_publish_qos0_works()
        {
            (PacketReplayer packetReplayer, IMqtt3Client client) = await Scenario.ConnectedClient( ClassCase, new[]
            {
                TestPacketHelper.Outgoing("3018000a7465737420746f70696374657374207061796c6f6164")
            } );

            await await client.PublishAsync( TestHelper.Monitor, new ApplicationMessage(
                "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.AtMostOnce, false )
            );
            await packetReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task simple_publish_qos1_works()
        {
            (PacketReplayer packetReplayer, IMqtt3Client client) = await Scenario.ConnectedClient( ClassCase, new[]
            {
                TestPacketHelper.Outgoing("321a000a7465737420746f706963000174657374207061796c6f6164"),
                TestPacketHelper.SendToClient("40020001")
            } );

            await await client.PublishAsync( TestHelper.Monitor, new ApplicationMessage(
                "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.AtLeastOnce, false )
            );

            await packetReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task simple_publish_qos2_works()
        {
            (PacketReplayer packetReplayer, IMqtt3Client client) = await Scenario.ConnectedClient( ClassCase, new[]
            {
                TestPacketHelper.Outgoing("341a000a7465737420746f706963000174657374207061796c6f6164"),
                TestPacketHelper.SendToClient("50020001"),
                TestPacketHelper.Outgoing("62020001"),
                TestPacketHelper.SendToClient("70020001")
            } );

            await await client.PublishAsync( TestHelper.Monitor, new ApplicationMessage(
                "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.ExactlyOnce, false )
            );
            await packetReplayer.StopAndEnsureValidAsync();
        }

        [Test]
        public async Task can_publish_topic_of_max_size()
        {
            using( CancellationTokenSource cts = new() )
            {

                (PacketReplayer packetReplayer, IMqtt3Client client) = await Scenario.ConnectedClient( ClassCase, new[]
                {
                    TestPacketHelper.SwallowEverything(cts.Token),
                } );
                await await client.PublishAsync( TestHelper.Monitor, new string( 'a', ushort.MaxValue ), QualityOfService.AtMostOnce, false, Array.Empty<byte>() );
                cts.Cancel();
            }
        }

        [Test]
        public async Task cannot_publish_topic_larger_than_ushort()
        {
            using( CancellationTokenSource cts = new() )
            {

                (PacketReplayer packetReplayer, IMqtt3Client client) = await Scenario.ConnectedClient( ClassCase, new[]
                {
                    TestPacketHelper.SwallowEverything(cts.Token),
                } );
                try
                {
                    await await client.PublishAsync( TestHelper.Monitor, new string( 'a', ushort.MaxValue + 1 ), QualityOfService.AtMostOnce, false, Array.Empty<byte>() );
                    Assert.Fail();
                }
                catch( Exception )
                {

                }
                cts.Cancel();
            }
        }


    }
}
