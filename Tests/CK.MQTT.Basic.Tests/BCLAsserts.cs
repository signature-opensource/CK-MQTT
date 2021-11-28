using FluentAssertions;
using NUnit.Framework;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Abstractions.Tests
{
    /// <summary>
    /// Simple assertions on the behavior of the BCL.
    /// </summary>
    [ExcludeFromCodeCoverage]
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
        public async Task ReadAtLeastAsync_works()
        {
            Pipe pipe = new( new PipeOptions( minimumSegmentSize: 1 ) );
            Task task = Task.Run( async () =>
            {
                for ( int i = 0; i<26;i++ )
                {
                    await pipe.Writer.WriteAsync( new byte[1] );
                    await pipe.Writer.FlushAsync();
                    await Task.Delay( 1 );
                }
            } );
            ReadResult readResult = await pipe.Reader.ReadAtLeastAsync( 26 );
            readResult.Buffer.Length.Should().Be( 26 );
        }
    }
}
