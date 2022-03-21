using CK.Core;
using CK.MQTT.Client.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests
{
    [ExcludeFromCodeCoverage]
    public class AutoReconnectTests_PipeReaderCop : AutoReconnectTests
    {
        public override string ClassCase => "PipeReaderCop";
    }

    [ExcludeFromCodeCoverage]
    public class AutoReconnectTests_Default : AutoReconnectTests
    {
        public override string ClassCase => "Default";
    }

    [ExcludeFromCodeCoverage]
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
            //Doesn't reconnect if the first connect fails.
            await Scenario.RunOnConnectedClientWithKeepAlive( ClassCase, new[]
            {
                TestPacketHelper.Disconnect,
                TestPacketHelper.IncrementTime(TimeSpan.FromSeconds(6)),
                async (m, replayer) =>
                {
                    await Task.Delay(1000);
                    replayer.Client.IsConnected.Should().BeFalse();
                    return true;
                },
                TestPacketHelper.WaitClientConnected,
                TestPacketHelper.Outgoing("101600044d51545404020000000a434b4d71747454657374"),
                TestPacketHelper.SendToClient("20020000"),
                (m, replayer) =>
                {
                    replayer.Client.IsConnected.Should().BeTrue();
                    return new ValueTask<bool>(true);
                }
            }, DisconnectBehavior.AutoReconnect );
        }
    }
}
