using CK.MQTT.Client.Tests.Helpers;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests
{
    public class PingTests_PipeReaderCop : PingTests
    {
        public override string ClassCase => "PipeReaderCop";
    }

    public class PingTests_Default : PingTests
    {
        public override string ClassCase => "Default";
    }

    public class PingTests_BytePerByteChannel : PingTests
    {
        public override string ClassCase => "BytePerByte";
    }

    public abstract class PingTests
    {
        public abstract string ClassCase { get; }

        [Test]
        public async Task normal_ping_works()
        {
            var replayer = new PacketReplayer( ClassCase );
            var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfigWithKeepAlive( replayer ) );
            await replayer.ConnectClient( TestHelper.Monitor, client );
            replayer.TestDelayHandler.IncrementTime( TimeSpan.FromSeconds( 5 ) );
            await Task.Delay( 50 );
            await replayer.AssertClientSent( TestHelper.Monitor, "C0" );
        }
    }
}
