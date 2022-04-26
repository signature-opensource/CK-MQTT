using CK.MQTT.Client.Tests.Helpers;
using NUnit.Framework;
using System;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests
{
    public class PublishTests_PipeReaderCop : ConnectionTests
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
        public async Task simple_publish_qos0_works()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClient( TestHelper.Monitor, client );

            await await client.PublishAsync( new ApplicationMessage(
               "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.AtMostOnce, false )
           );
            await replayer.AssertClientSent( TestHelper.Monitor, "3018000a7465737420746f70696374657374207061796c6f6164" );
        }

        [Test]
        public async Task simple_publish_qos1_works()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClient( TestHelper.Monitor, client );

            var task = await client.PublishAsync( new ApplicationMessage(
                "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.AtLeastOnce, false )
            );
            await replayer.AssertClientSent( TestHelper.Monitor, "321a000a7465737420746f706963000174657374207061796c6f6164" );
            await replayer.SendToClient( TestHelper.Monitor, "40020001" );
            await task;
        }

        [Test]
        public async Task simple_publish_qos2_works()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClient( TestHelper.Monitor, client );

            var task = await client.PublishAsync( new ApplicationMessage(
                "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.ExactlyOnce, false )
            );
            await replayer.AssertClientSent( TestHelper.Monitor, "341a000a7465737420746f706963000174657374207061796c6f6164" );
            await replayer.SendToClient( TestHelper.Monitor, "50020001" );
            await replayer.AssertClientSent( TestHelper.Monitor, "62020001" );
            await replayer.SendToClient( TestHelper.Monitor, "70020001" );
            await task;
        }

        [Test]
        public async Task can_publish_topic_of_max_size()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClient( TestHelper.Monitor, client );

            await await client.PublishAsync( new string( 'a', ushort.MaxValue ), QualityOfService.AtMostOnce, false, Array.Empty<byte>() );
        }

        [Test]
        public async Task cannot_publish_topic_larger_than_ushort()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClient( TestHelper.Monitor, client );

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
            (PacketReplayer replayer, TestMqttClient client) = Connect().GetAwaiter().GetResult();
            for( int i = 0; i < 10000; i++ )
            {
                var _ = client.PublishAsync( new ApplicationMessage(
               "test topic", Encoding.UTF8.GetBytes( "test payload" ), QualityOfService.AtMostOnce, false )
                ).AsTask().GetAwaiter().GetResult();
            }
            Check( replayer ).GetAwaiter().GetResult();
        }

        async Task Check( PacketReplayer replayer )
        {
            for( int i = 0; i < 1000; i++ )
            {
                await replayer.AssertClientSent( TestHelper.Monitor, "3018000a7465737420746f70696374657374207061796c6f6164" );
            }
        }

        async Task<(PacketReplayer, TestMqttClient)> Connect()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
            await replayer.ConnectClient( TestHelper.Monitor, client );
            return (replayer, client);
        }
    }
}
