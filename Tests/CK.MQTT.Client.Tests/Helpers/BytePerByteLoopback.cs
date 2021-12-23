using CK.MQTT.Client.Tests.Helpers;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests
{
    [ExcludeFromCodeCoverage]
    class BytePerBytePipeReader : PipeReader
    {
        readonly PipeReader _pr;
        public BytePerBytePipeReader( PipeReader pr )
        {
            _pr=pr;
        }

        int _readCount = 1;

        public override Task CopyToAsync( PipeWriter destination, CancellationToken cancellationToken = default )
            => throw new NotSupportedException();

        public override Task CopyToAsync( Stream destination, CancellationToken cancellationToken = default )
            => throw new NotSupportedException();

        [Obsolete]
        public override void OnWriterCompleted( Action<Exception?, object?> callback, object? state )
            => throw new NotSupportedException();

        protected override async ValueTask<ReadResult> ReadAtLeastAsyncCore( int minimumSize, CancellationToken cancellationToken )
        {
            ReadResult res = await _pr.ReadAtLeastAsync( minimumSize, cancellationToken );
            _lastReadResult = res;

            return res;
        }

        public override ValueTask CompleteAsync( Exception? exception = null )
            => _pr.CompleteAsync( exception );

        public override Stream AsStream( bool leaveOpen = false )
            => _pr.AsStream( leaveOpen );

        public override void AdvanceTo( SequencePosition consumed )
        {
            if( _lastReadResult.Buffer.Slice( 0, consumed ).Length == 0 ) throw new InvalidOperationException( "Advancing of 0 bytes. You have a bug there." );
            _readCount = 1;
            _pr.AdvanceTo( consumed );
        }

        public override void AdvanceTo( SequencePosition consumed, SequencePosition examined )
        {
            if( _lastReadResult.Buffer.Slice( 0, examined ).Length == 0 ) throw new InvalidOperationException( "Advancing of 0 bytes. You have a bug there." );
            if( _lastReadResult.Buffer.Slice( 0, consumed ).Length > 0 )
            {
                _readCount = 1;
            }
            _pr.AdvanceTo( consumed, examined );
        }

        public override void CancelPendingRead()
        {
            _lastReadResult = default;
            _pr.CancelPendingRead();
        }

        public override void Complete( Exception? exception = null )
        {
            _lastReadResult = default;
            _pr.Complete( exception );
        }

        ReadResult _lastReadResult;

        public override async ValueTask<ReadResult> ReadAsync( CancellationToken cancellationToken = default )
        {
            ReadResult res = await _pr.ReadAsync( cancellationToken );
            if( res.Buffer.Length > _readCount )
            {
                res = new ReadResult( res.Buffer.Slice( 0, _readCount ), false, false );
                _readCount++;
            }
            _lastReadResult = res;
            return res;
        }

        public override bool TryRead( out ReadResult result )
        {
            bool res = _pr.TryRead( out result );
            if( result.Buffer.Length > _readCount )
            {
                result = new ReadResult( result.Buffer.Slice( 0, _readCount ), false, false );
                _readCount++;
            }
            _lastReadResult = result;
            return res;
        }
    }

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
            Pipe output = new( new PipeOptions( minimumSegmentSize: size, pool: new SingleBytePool() ) );

            DuplexPipe = new DuplexPipe( new BytePerBytePipeReader( input.Reader ), output.Writer );
            TestDuplexPipe = new DuplexPipe( output.Reader, input.Writer );
        }

        public override IDuplexPipe TestDuplexPipe { get; set; }
        public override IDuplexPipe DuplexPipe { get; set; }


        public override void Close( IInputLogger? m )
        {
        
            TestDuplexPipe.Input.Complete();
            TestDuplexPipe.Output.Complete();
        }
    }
}
