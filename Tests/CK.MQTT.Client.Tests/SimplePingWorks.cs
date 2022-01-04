using CK.MQTT.Client.Tests.Helpers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests
{
    [ExcludeFromCodeCoverage]
    public class PingTests_PipeReaderCop : PingTests
    {
        public override string ClassCase => "PipeReaderCop";
    }

    [ExcludeFromCodeCoverage]
    public class PingTests_Default : PingTests
    {
        public override string ClassCase => "Default";
    }

    [ExcludeFromCodeCoverage]
    public class PingTests_BytePerByteChannel : PingTests
    {
        public override string ClassCase => "BytePerByte";
    }

    [ExcludeFromCodeCoverage]
    public abstract class PingTests
    {
        public abstract string ClassCase { get; }

        [Test]
        public async Task normal_ping_works()
        {
            await Scenario.RunOnConnectedClientWithKeepAlive( ClassCase, new[]
            {
                    TestPacketHelper.IncrementTime(TimeSpan.FromSeconds(5)),
                    TestPacketHelper.Outgoing("30")
            } );
        }
    }
}
