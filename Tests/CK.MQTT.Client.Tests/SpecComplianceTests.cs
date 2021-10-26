using CK.MQTT.Client.Tests.Helpers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests
{
    public class SpecComplianceTests
    {
        [Test]
        public async Task can_publish_topic_of_max_size()
        {
            using( CancellationTokenSource cts = new() )
            {

                (PacketReplayer packetReplayer, IMqtt3Client client) = await Scenario.ConnectedClient( new[]
                {
                    TestPacketHelper.SwallowEverything(cts.Token),
                } );
                await await client.PublishAsync( TestHelper.Monitor, new string( 'a', ushort.MaxValue ), QualityOfService.AtMostOnce, false, new byte[0] );
                cts.Cancel();
            }
        }

        [Test]
        public async Task cannot_publish_topic_larger_than_ushort()
        {
            using( CancellationTokenSource cts = new() )
            {

                (PacketReplayer packetReplayer, IMqtt3Client client) = await Scenario.ConnectedClient( new[]
                {
                    TestPacketHelper.SwallowEverything(cts.Token),
                } );
                try
                {
                    await await client.PublishAsync( TestHelper.Monitor, new string( 'a', ushort.MaxValue + 1 ), QualityOfService.AtMostOnce, false, new byte[0] );
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
