using System.Buffers;
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

        public static bool TryReadUtf8String( ref this SequenceReader<byte> reader, int length, [NotNullWhen( true )] out string? value )
        {
            var unreadSeq = reader.UnreadSequence;
            if( unreadSeq.Length < length )
            {
                value = null;
                return false;
            }
            unreadSeq = reader.UnreadSequence.Slice( 0, length );
            value = Encoding.UTF8.GetString( in unreadSeq );
            reader.Advance( length );
            return true;
        }
    }
}
