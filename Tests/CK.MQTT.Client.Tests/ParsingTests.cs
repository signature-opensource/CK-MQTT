using CK.Core;
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
    public class ParsingTests_Default : ParsingTests
    {
        public override string ClassCase => "Default";
    }

    public class ParsingTests_BytePerByteChannel : ParsingTests
    {
        public override string ClassCase => "BytePerByte";
    }
    public abstract class ParsingTests
    {
        public abstract string ClassCase { get; }

        [Test]
        public async Task can_read_maxsized_topic()
        {
            TaskCompletionSource tcs = new();

            StringBuilder sb = new StringBuilder()
                .Append( "30818004FFFF" );
            for( int i = 0; i < ushort.MaxValue; i++ )
            {
                sb.Append( "61" );
            }
            (PacketReplayer packetReplayer, IMqtt3Client client) = await Scenario.ConnectedClient( ClassCase, new[]
            {
                TestPacketHelper.SendToClient(sb.ToString()),
            }, messageProcessor: ( m, msg, token ) =>
            {
                tcs.SetResult();
                msg.Dispose();
                return new ValueTask();
            } );
            await tcs.Task;
            await packetReplayer.StopAndEnsureValidAsync();
        }
    }
}
