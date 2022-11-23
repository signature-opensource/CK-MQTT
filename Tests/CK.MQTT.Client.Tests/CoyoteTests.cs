using System;
using System.Text;
using System.Threading.Tasks;
using CK.Core;
using CK.MQTT.Client.Tests.Helpers;
using CK.Testing;
using Microsoft.Coyote;
using Microsoft.Coyote.SystematicTesting;

namespace CK.MQTT.Client.Tests
{
    public class CoyoteTests
    {
        [Test]
        public static void CoyoteTestTask()
        {
            Environment.CurrentDirectory = "C:\\dev\\CK-MQTT\\Tests\\CK.MQTT.Client.Tests";
            var configuration = Configuration.Create().WithTestingIterations( 10 );
            var engine = TestingEngine.Create( configuration, simple_publish_qos2_works_Async );
            engine.Run();
        }

        public static async Task simple_publish_qos2_works_Async()
        {
            var m = new ActivityMonitor();
            var replayer = new PacketReplayer( "Default" );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClientAsync( m, client );

            var task = await client.PublishAsync( new ApplicationMessage(
                "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.ExactlyOnce, false )
            );
            await replayer.AssertClientSentAsync( m, "341a000a7465737420746f706963000174657374207061796c6f6164" );
            await replayer.SendToClientAsync( m, "50020001" );
            await replayer.AssertClientSentAsync( m, "62020001" );
            await replayer.SendToClientAsync( m, "70020001" );
            await task;
        }
    }
}
