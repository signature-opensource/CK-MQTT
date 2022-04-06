using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace CK.MQTT
{
    /// <summary>
    /// Simple helper class on <see cref="SequenceReader{T}"/>.
    /// I expect that a more efficient/less buggy version of these functions will be part of SequenceReader API someday.
    /// </summary>
    public static class SequenceReaderExtensions
    {
        /// <summary>
        /// <see cref="SequenceReaderExtensions.TryReadBigEndian(ref SequenceReader{byte}, out short)"/> but casted to <see cref="ushort"/>
        /// </summary>
        /// <param name="sequenceReader"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool TryReadBigEndian( this ref SequenceReader<byte> sequenceReader, out ushort value )
        {
            bool status = sequenceReader.TryReadBigEndian( out short toCast );
            value = (ushort)toCast;
            return status;
        }

        public static bool TryReadBigEndian( this ref SequenceReader<byte> sequenceReader, out uint value )
        {
            bool status = sequenceReader.TryReadBigEndian( out int toCast );
            value = (uint)toCast;
            return status;
        }

        /// <summary>
        /// Copy and Paste of https://github.com/dotnet/runtime/issues/29318#issuecomment-484987895
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="length"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool TryReadUtf8String( ref this SequenceReader<byte> reader, int length, [NotNullWhen( true )] out string? value )
        {
            ReadOnlySpan<byte> span = reader.UnreadSpan;
            if( span.Length < length )
                return TryReadMultisegmentUtf8String( ref reader, length, out value );

            ReadOnlySpan<byte> slice = span.Slice( 0, length );
            value = Encoding.UTF8.GetString( slice );
            reader.Advance( length );
            return true;
        }

        /// <summary>
        /// Copy and Paste of https://github.com/dotnet/runtime/issues/29318#issuecomment-484987895
        /// </summary>
        static bool TryReadMultisegmentUtf8String( ref SequenceReader<byte> reader, int length, [NotNullWhen( true )] out string? value )
        {
            Debug.Assert( reader.UnreadSpan.Length < length );
            var unreadSeq = reader.UnreadSequence;
            if( unreadSeq.Length < length )
            {
                value = null;
                return false;
            }
            unreadSeq = reader.UnreadSequence.Slice( 0, length );

            value = Encoding.UTF8.GetString( in unreadSeq );
            return true;
        }
    }
}
