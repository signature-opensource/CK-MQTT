using CK.MQTT.Client.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests
{
    public class AutoReconnectTests_PipeReaderCop : AutoReconnectTests
    {
        public override string ClassCase => "PipeReaderCop";
    }

    public class AutoReconnectTests_Default : AutoReconnectTests
    {
        public override string ClassCase => "Default";
    }

    public class AutoReconnectTests_BytePerByteChannel : AutoReconnectTests
    {
        public override string ClassCase => "BytePerByte";
    }
    public abstract class AutoReconnectTests
    {
        public abstract string ClassCase { get; }

        [Test]
        public async Task auto_reconnect_works()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfigWithKeepAlive( replayer, disconnectBehavior: DisconnectBehavior.AutoReconnect ) );
            await replayer.ConnectClient( TestHelper.Monitor, client );
            //Doesn't reconnect if the first connect fails.
            replayer.TestDelayHandler.IncrementTime( TimeSpan.FromSeconds( 6 ) );
            await Task.Delay( 1000 );
            replayer.TestDelayHandler.IncrementTime( TimeSpan.FromSeconds( 6 ) );
            await replayer.ShouldContainEventAsync<LoopBack.DisposedChannel>();
            await replayer.ShouldContainEventAsync<TestMqttClient.UnattendedDisconnect>();
            await replayer.ShouldContainEventAsync<PacketReplayer.CreatedChannel>();

            await replayer.AssertClientSent( TestHelper.Monitor, "101600044d51545404020005000a434b4d71747454657374" );
            await replayer.SendToClient( TestHelper.Monitor, "20020000" );
            await Task.Delay( 1000 );
        }
    }
}
