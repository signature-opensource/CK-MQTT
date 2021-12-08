using CK.MQTT.Client.Tests.Helpers;
using NUnit.Framework;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests
{
    [ExcludeFromCodeCoverage]
    public class SerializingTests_PipeReaderCop : ConnectionTests
    {
        public override string ClassCase => "PipeReaderCop";
    }

    [ExcludeFromCodeCoverage]
    public class SerializingTests_Default : SerializingTests
    {
        public override string ClassCase => "Default";
    }

    [ExcludeFromCodeCoverage]
    public class SerializingTests_BytePerByteChannel : SerializingTests
    {
        public override string ClassCase => "BytePerByte";
    }

    [ExcludeFromCodeCoverage]
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
