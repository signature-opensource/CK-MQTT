using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests.Helpers
{
    class PipeReaderCop : PipeReader
    {
        readonly PipeReader _pr;

        public PipeReaderCop( PipeReader pr ) => _pr = pr;


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
            _pr.AdvanceTo( consumed );
        }

        public override void AdvanceTo( SequencePosition consumed, SequencePosition examined )
        {
            if( _lastReadResult.Buffer.Slice( 0, examined ).Length == 0 ) throw new InvalidOperationException( "Advancing of 0 bytes. You have a bug there." );
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
            _lastReadResult = res;
            return res;
        }

        public override bool TryRead( out ReadResult result )
        {
            bool res = _pr.TryRead( out result );
            _lastReadResult = result;
            return res;
        }
    }
}
