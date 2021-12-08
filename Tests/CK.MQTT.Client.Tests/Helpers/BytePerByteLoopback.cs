using CK.MQTT.Client.Tests.Helpers;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests
{
    [ExcludeFromCodeCoverage]
    public class BytePerByteLoopback : LoopBack
    {
        class SingleBytePool : MemoryPool<byte>
        {
            class MemoryOwner : IMemoryOwner<byte>
            {
                public Memory<byte> Memory { get; set; }

                public void Dispose()
                {
                }
            }
            public override int MaxBufferSize => 1;

            public override IMemoryOwner<byte> Rent( int minBufferSize = -1 )
            {
                if( minBufferSize != 1 )
                {
                    throw new NotSupportedException();
                }
                return new MemoryOwner()
                {
                    Memory = new byte[1]
                };
            }

            protected override void Dispose( bool disposing )
            {
            }
        }
        public BytePerByteLoopback()
        {
            const int size = 16;
            Pipe input = new( new PipeOptions( minimumSegmentSize: size, pool: new SingleBytePool() ) );
            Pipe intermediary = new( new PipeOptions( minimumSegmentSize: size, pool: new SingleBytePool() ) );
            Pipe output = new( new PipeOptions( minimumSegmentSize: size, pool: new SingleBytePool() ) );

            _ = WorkLoop( input.Writer, input.Reader, intermediary.Writer );
            DuplexPipe = new DuplexPipe( intermediary.Reader, output.Writer );
            TestDuplexPipe = new DuplexPipe( output.Reader, input.Writer );

        }

        readonly byte[] _buffer = new byte[1];

        public override IDuplexPipe TestDuplexPipe { get; set; }
        public override IDuplexPipe DuplexPipe { get; set; }

        async Task WorkLoop( PipeWriter inputWriter, PipeReader inputReader, PipeWriter bytePerByte )
        {
            while( !_cts.IsCancellationRequested )
            {
                ReadResult result = await inputReader.ReadAsync( _cts.Token );
                if( result.Buffer.Length > 0 )
                {
                    ReadOnlySequence<byte> sliced = result.Buffer.Slice( 0, 1 );
                    sliced.CopyTo( _buffer );
                    await bytePerByte.WriteAsync( _buffer );
                    await bytePerByte.FlushAsync();
                    inputReader.AdvanceTo( sliced.End );
                }

                if( result.IsCompleted ) break;
                if( result.IsCanceled ) break;
                if( result.Buffer.IsEmpty ) throw new InvalidOperationException( "Buffer is empty, but IsCompleted & IsCanceled are false." );
            }

            if( inputReader.TryRead( out ReadResult result2 ) )
            {
                ReadOnlySequence<byte> buffer = result2.Buffer;
                while( buffer.Length > 0 )
                {
                    ReadOnlySequence<byte> sliced = buffer.Slice( 0, 1 );
                    sliced.CopyTo( _buffer );
                    buffer = buffer.Slice( 1 );
                    await bytePerByte.WriteAsync( _buffer );
                    await bytePerByte.FlushAsync();
                }
                inputReader.AdvanceTo( buffer.End );
            }

            bytePerByte.Complete();
            inputWriter.Complete();
        }

        readonly CancellationTokenSource _cts = new();

        public override void Close( IInputLogger? m )
        {
            _cts.Cancel();
        }
    }
}
