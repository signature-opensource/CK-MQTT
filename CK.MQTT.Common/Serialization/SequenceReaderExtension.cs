using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.MQTT.Common.Serialisation
{
    public enum SequenceReadResult
    {
        Ok = 1,
        NotEnoughBytes = 2,
        CorruptedStream = 4,
    }
    public static class SequenceReaderExtension
    {
        /// <summary>
        /// Buffer must contain packet type.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="length">The Remaining Length, -1 if there is no enough bytes to read, -2 if the stream is corrupted.</param>
        /// <returns></returns>
        public static SequenceReadResult TryReadRemainingLength( this ref SequenceReader<byte> sequenceReader, out int length )
        {
            length = 0;
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            int shift = 0;
            byte b;
            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                if( shift == 5 * 7 ) return SequenceReadResult.CorruptedStream; // 5 bytes max per Int32, shift += 7
                                                                                // ReadByte handles end of stream cases for us.
                if( !sequenceReader.TryRead( out b ) )
                {
                    sequenceReader.Rewind( 1 + shift / 7 );
                    return SequenceReadResult.NotEnoughBytes;
                }
                length |= (b & 0x7F) << shift;
                shift += 7;
            } while( (b & 0x80) != 0 );
            return SequenceReadResult.Ok;
        }

        public static short ReadInt16( this ref SequenceReader<byte> sequenceReader )
        {
            if( !sequenceReader.TryReadBigEndian( out short value ) ) throw new IndexOutOfRangeException();
            return value;
        }

        public static ushort ReadUInt16( this ref SequenceReader<byte> sequenceReader )
        {
            if( !sequenceReader.TryReadBigEndian( out short value ) ) throw new IndexOutOfRangeException();
            return (ushort)value;
        }

        public static byte Read( this ref SequenceReader<byte> sequenceReader )
        {
            if( !sequenceReader.TryRead( out byte value ) ) throw new IndexOutOfRangeException();
            return value;
        }

        /// <summary>
        /// Read <see cref="SequenceReader{T}.UnreadSpan"/> and advance the reader of the given length.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static ReadOnlySpan<byte> ReadCurrentUnreadSpan( this ref SequenceReader<byte> reader, int count )
        {
            ReadOnlySpan<byte> val = reader.UnreadSpan;
            int length = val.Length > count ? count : val.Length;
            reader.Advance( length );
            return val[..length];
        }

        public static string ReadString( this ref SequenceReader<byte> reader, int byteCount, Encoding? encoding = null )
        {
            StringBuilder stringBuilder = new StringBuilder();
            Decoder decoder = (encoding ?? Encoding.UTF8).GetDecoder();
            while( byteCount > 0 )
            {
                ReadOnlySpan<byte> currSpan = reader.ReadCurrentUnreadSpan( byteCount );
                byteCount -= currSpan.Length;
                bool final = byteCount == 0;
                int charCount = decoder.GetCharCount( currSpan, final );
                Span<char> buffer = stackalloc char[charCount];
                int output = decoder.GetChars( currSpan, buffer, final );
                Debug.Assert( output == charCount );
                stringBuilder.Append( buffer );
            }
            return stringBuilder.ToString();
        }
    }
}
