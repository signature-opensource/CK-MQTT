using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Core.Extension
{
    public static class PipeReaderExtensions
    {
        /// <summary>
        /// Represent if the fill was succesfull, or if it's an error.
        /// </summary>
        public enum FillStatus
        {
            /// <summary>
            /// The fill was successfull and the buffer has been clompletly filled.
            /// </summary>
            Done,
            /// <summary>
            /// Operation has been canceled, the buffer is empty or partially filled.
            /// </summary>
            Canceled,
            /// <summary>
            /// Unexpected end of stream, the buffer is empty or partially filled.
            /// </summary>
            UnexpectedEndOfStream
        }

        /// <summary>
        /// Fill the given buffer with the data from the given <see cref="PipeReader"/>
        /// </summary>
        /// <param name="reader">The <see cref="PipeReader"/> to read data from.</param>
        /// <param name="buffer">The buffer to fill.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>An enum representing the status of the the operation.</returns>
        public static async ValueTask<FillStatus> CopyToBufferAsync( this PipeReader reader, Memory<byte> buffer, CancellationToken cancellationToken )
        {
            Debug.Assert( !buffer.IsEmpty );
            while( true )
            {
                ReadResult result = await reader.ReadAsync( cancellationToken );
                ReadOnlySequence<byte> readBuffer = result.Buffer;
                if( readBuffer.Length > buffer.Length ) readBuffer = readBuffer.Slice( 0, buffer.Length );//slice the pipe buffer if bigger than target.
                readBuffer.CopyTo( buffer.Span );//copy data to the input buffer.
                buffer = buffer.Slice( (int)readBuffer.Length );//then truncate the input buffer, so we don't overwrite our data.
                reader.AdvanceTo( readBuffer.End );//and don't forget to signal that we consumed the data.
                if( buffer.Length == 0 ) return FillStatus.Done;
                if( result.IsCanceled || cancellationToken.IsCancellationRequested ) return FillStatus.Canceled;
                if( result.IsCompleted ) return FillStatus.UnexpectedEndOfStream;
            }
        }
    }
}
