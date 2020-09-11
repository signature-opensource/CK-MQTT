using FluentAssertions;
using NUnit.Framework;
using System;
using System.IO;
using System.IO.Pipelines;

namespace CK.MQTT.Client.Abstractions.Tests
{
    public class PipeWriterAsserts
    {
        [Test]
        public void PipeWriter_second_memory_access_is_same()
        {
            MemoryStream mem = new MemoryStream();
            PipeWriter pw = PipeWriter.Create( mem );
            Span<byte> memory1 = pw.GetSpan( 1 );
            memory1[0] = 42;
            Span<byte> memory2 = pw.GetSpan( 1 );
            memory2[2].Should().Be( 42 );
        }
    }
}
