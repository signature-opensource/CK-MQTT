using FluentAssertions;
using NUnit.Framework;
using System;
using System.Buffers;
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
            memory2[0].Should().Be( 42 );
        }

        internal class MemorySegment<T> : ReadOnlySequenceSegment<T>
        {
            public MemorySegment( ReadOnlyMemory<T> memory )
            {
                Memory = memory;
            }

            public MemorySegment<T> Append( ReadOnlyMemory<T> memory )
            {
                var segment = new MemorySegment<T>( memory )
                {
                    RunningIndex = RunningIndex + Memory.Length
                };

                Next = segment;

                return segment;
            }
        }
        [Test]
        public void Test()
        {
            Memory<byte> seq1 = new byte[5];
            Memory<byte> seq2 = new byte[5];
            var memSeg = new MemorySegment<byte>( seq1 );
            var memSegEnd = memSeg.Append( seq2 );
            ReadOnlySequence<byte> seq = new ReadOnlySequence<byte>( memSeg, 0, memSegEnd, 5 );
            
        }
    }
}
