using System;
using System.Buffers;
using System.Collections.Generic;
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
        public static SequenceReadResult TryReadRemainingLength( this ref SequenceParser<byte> sequenceReader, out int length )
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
                    sequenceReader.Rewind( 1+ shift / 7 );
                    return SequenceReadResult.NotEnoughBytes;
                }
                length |= (b & 0x7F) << shift;
                shift += 7;
            } while( (b & 0x80) != 0 );
            return SequenceReadResult.Ok;
        }
    }
}
