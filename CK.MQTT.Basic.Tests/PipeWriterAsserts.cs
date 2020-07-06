using FluentAssertions;
using NUnit.Framework;
using System;
using System.IO;
using System.IO.Pipelines;
using System.Text;

namespace CK.MQTT.Client.Abstractions.Tests
{
    public class PipeWriterAsserts
    {
        [Test]
        public void PipeWriter_work_as_i_expected()
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
