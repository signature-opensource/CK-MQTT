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
    public class SerializingTests_Default : ConnectionTests
    {
        public override string ClassCase => "Default";
    }

    public class SerializingTests_BytePerByteChannel : ConnectionTests
    {
        public override string ClassCase => "BytePerByte";
    }

    public abstract class SerializingTests
    {
        public abstract string ClassCase { get; }

        [Test]
        public async Task packet_of_128_bytes_payload_serialized_correctly()
        {
            using( CancellationTokenSource cts = new() )
            {

                (PacketReplayer packetReplayer, IMqtt3Client client) = await Scenario.ConnectedClient( ClassCase, new[]
                {
                    TestPacketHelper.SwallowEverything(cts.Token),
                } );
                await await client.PublishAsync( TestHelper.Monitor, new string( 'a', 128 - 2/*packet id size*/ ), QualityOfService.AtMostOnce, false, Array.Empty<byte>() );
            }
        }
    }
}
