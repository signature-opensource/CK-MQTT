using CK.MQTT.Client.Tests.Helpers;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.MQTT.Client.Tests;

public class SerializingTests_PipeReaderCop : SerializingTests
{
    public override string ClassCase => "PipeReaderCop";
}

public class SerializingTests_Default : SerializingTests
{
    public override string ClassCase => "Default";
}

public class SerializingTests_BytePerByteChannel : SerializingTests
{
    public override string ClassCase => "BytePerByte";
}

public abstract class SerializingTests
{
    public abstract string ClassCase { get; }

    [Test]
    public async Task packet_of_128_bytes_payload_serialized_correctlyAsync()
    {
        var replayer = new PacketReplayer( ClassCase );
        var client = replayer.CreateMQTT3Client( TestConfigs.DefaultTestConfig( replayer ) );
        await replayer.ConnectClientAsync( TestHelper.Monitor, client );
        await await client.PublishAsync( new string( 'a', 128 - 2/*packet id size*/ ), Array.Empty<byte>(), QualityOfService.AtMostOnce, false );

        //TODO: add assert ?
    }
}
