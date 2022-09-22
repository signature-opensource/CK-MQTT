using CK.MQTT.Client.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests
{
    public class PublishTests_PipeReaderCop : PublishTests
    {
        public override string ClassCase => "PipeReaderCop";
    }

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
        public async Task simple_publish_qos0_works_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClientAsync( TestHelper.Monitor, client );

            await await client.PublishAsync( new ApplicationMessage(
               "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.AtMostOnce, false )
           );
            await replayer.AssertClientSentAsync( TestHelper.Monitor, "3018000a7465737420746f70696374657374207061796c6f6164" );
        }

        [Test]
        public async Task simple_publish_qos1_works_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClientAsync( TestHelper.Monitor, client );

            var task = await client.PublishAsync( new ApplicationMessage(
                "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.AtLeastOnce, false )
            );
            await replayer.AssertClientSentAsync( TestHelper.Monitor, "321a000a7465737420746f706963000174657374207061796c6f6164" );
            await replayer.SendToClientAsync( TestHelper.Monitor, "40020001" );
            await task;
        }

        [Test]
        public async Task simple_publish_qos2_works_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClientAsync( TestHelper.Monitor, client );

            var task = await client.PublishAsync( new ApplicationMessage(
                "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.ExactlyOnce, false )
            );
            await replayer.AssertClientSentAsync( TestHelper.Monitor, "341a000a7465737420746f706963000174657374207061796c6f6164" );
            await replayer.SendToClientAsync( TestHelper.Monitor, "50020001" );
            await replayer.AssertClientSentAsync( TestHelper.Monitor, "62020001" );
            await replayer.SendToClientAsync( TestHelper.Monitor, "70020001" );
            await task;
        }

        [Test]
        public async Task simple_qos2_receive_works_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClientAsync( TestHelper.Monitor, client );


            await replayer.SendToClientAsync( TestHelper.Monitor, "341a000a7465737420746f706963000174657374207061796c6f6164" );
            await replayer.AssertClientSentAsync( TestHelper.Monitor, "50020001" );
            await replayer.SendToClientAsync( TestHelper.Monitor, "62020001" );
            await replayer.AssertClientSentAsync( TestHelper.Monitor, "70020001" );
        }

        [Test]
        public async Task send_qos2_double_pubrel_works_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClientAsync( TestHelper.Monitor, client );
            var task = await client.PublishAsync( new ApplicationMessage(
               "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.ExactlyOnce, false )
            );
            await replayer.AssertClientSentAsync( TestHelper.Monitor, "341a000a7465737420746f706963000174657374207061796c6f6164" );
            await replayer.SendToClientAsync( TestHelper.Monitor, "50020001" );
            await replayer.AssertClientSentAsync( TestHelper.Monitor, "62020001" );
            replayer.TestTimeHandler.IncrementTime( TimeSpan.FromSeconds( 5 ) ); // some lag
            await replayer.AssertClientSentAsync( TestHelper.Monitor, "62020001" );  // client will resend the packet

            await replayer.SendToClientAsync( TestHelper.Monitor, "70020001" ); // 1st packet response
            await task; // task should be resolved here.
            // client should be able to ingest this response.
            await replayer.SendToClientAsync( TestHelper.Monitor, "70020001" ); // 2nd packet response
            await Task.Delay( 50 ); // to avoid concurrency issues.
            client.IsConnected.Should().BeTrue();

        }

        [Test]
        public async Task can_publish_topic_of_max_size_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClientAsync( TestHelper.Monitor, client );

            await await client.PublishAsync( new string( 'a', ushort.MaxValue ), QualityOfService.AtMostOnce, false, Array.Empty<byte>() );
        }

        [Test]
        public async Task cannot_publish_topic_larger_than_ushort_Async()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClientAsync( TestHelper.Monitor, client );

            try
            {
                await await client.PublishAsync( new string( 'a', ushort.MaxValue + 1 ), QualityOfService.AtMostOnce, false, Array.Empty<byte>() );
                Assert.Fail();
            }
            catch( Exception )
            {

            }
        }

        [Test]
        public void publish_async_over_sync_does_not_deadlock()
        {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            (PacketReplayer replayer, TestMQTTClient client) = ConnectAsync().GetAwaiter().GetResult();
            for( int i = 0; i < 10000; i++ )
            {
                var _ = client.PublishAsync( new ApplicationMessage(
               "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.AtMostOnce, false )
                ).AsTask().GetAwaiter().GetResult();
            }
            Check().GetAwaiter().GetResult();
            async Task Check()
            {
                for( int i = 0; i < 1000; i++ )
                {
                    await replayer.AssertClientSentAsync( TestHelper.Monitor, "3018000a7465737420746f70696374657374207061796c6f6164" );
                }
            }
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
        }



        async Task<(PacketReplayer, TestMQTTClient)> ConnectAsync()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClientAsync( TestHelper.Monitor, client );
            return (replayer, client);
        }
    }
}
