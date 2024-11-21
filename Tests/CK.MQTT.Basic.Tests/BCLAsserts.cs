using FluentAssertions;
using NUnit.Framework;
using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Abstractions.Tests;

/// <summary>
/// Simple assertions on the behavior of the BCL.
/// </summary>
public class BCLAsserts
{
    [Test]
    public void PipeWriter_second_memory_access_is_same()
    {
        MemoryStream mem = new();
        PipeWriter pw = PipeWriter.Create( mem );
        Span<byte> memory1 = pw.GetSpan( 1 );
        memory1[0] = 42;
        Span<byte> memory2 = pw.GetSpan( 1 );
        memory2[0].Should().Be( 42 );
    }

    [Test]
    public void CancellationTokenSource_can_be_cancelled_multiples_times()
    {
        CancellationTokenSource cts = new();
        cts.Cancel();
        cts.Cancel();
    }

    [Test]
    public void StreamPipeReader_ReadAtLeastAsync0()
    {
        MemoryStream mem = new();
        var piperReader = PipeReader.Create( mem );
        ValueTask<ReadResult> task = piperReader.ReadAtLeastAsync( 0 );
        task.IsCompleted.Should().BeTrue();
    }

    //[Test]
    //public void DefaultPipeReader_ReadAtLeastAsync0()
    //{
    //    var pipe = new Pipe();
    //    var piperReader = pipe.Reader;
    //    ValueTask<ReadResult> task = piperReader.ReadAtLeastAsync( 0 );
    //    task.IsCompleted.Should().BeTrue();
    //}
}
