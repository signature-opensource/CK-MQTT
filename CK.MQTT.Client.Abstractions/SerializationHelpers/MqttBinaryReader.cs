using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Serialisation
{
    public static class MqttBinaryReader
    {
        /// <summary>
        /// Buffer must contain packet type.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="length">The Remaining Length, -1 if there is no enough bytes to read, -2 if the stream is corrupted.</param>
        /// <returns></returns>
        public static OperationStatus TryReadMQTTRemainingLength( this ref SequenceReader<byte> sequenceReader, out int length )
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
                if( shift == 5 * 7 ) return OperationStatus.InvalidData; // 5 bytes max per Int32, shift += 7
                // ReadByte handles end of stream cases for us.

                if( !sequenceReader.TryRead( out b ) ) return OperationStatus.NeedMoreData;
                length |= (b & 0x7F) << shift;
                shift += 7;
            } while( (b & 0x80) != 0 );
            return OperationStatus.Done;
        }

        public static bool TryReadMQTTString(
            this ref SequenceReader<byte> reader,
            [NotNullWhen( true )] out string? output )
        {
            if( !reader.TryReadBigEndian( out ushort size ) )
            {
                output = null;
                return false;
            }
            return reader.TryReadUtf8String( size, out output );
        }

        static bool TryReadMQTTString( ReadOnlySequence<byte> buffer,
            [NotNullWhen( true )] out string? output, out SequencePosition sequencePosition )
        {
            SequenceReader<byte> reader = new SequenceReader<byte>( buffer );
            bool result = reader.TryReadMQTTString( out output );
            sequencePosition = reader.Position;
            return result;
        }

        public static async ValueTask<string> ReadMQTTString( this PipeReader pipeReader )
        {
            Beginning:
            ReadResult result = await pipeReader.ReadAsync();
            if( result.IsCanceled ) throw new OperationCanceledException();
            if( !TryReadMQTTString( result.Buffer, out string? output, out SequencePosition sequencePosition ) )
            {
                pipeReader.AdvanceTo( result.Buffer.Start, result.Buffer.End );
                if( result.IsCompleted ) throw new EndOfStreamException();
                goto Beginning;
            }
            pipeReader.AdvanceTo( sequencePosition );
            return output;
        }

        public static bool TryReadMQTTPayload( this ref SequenceReader<byte> reader, out ReadOnlySequence<byte> output )
        {
            if( !reader.TryReadBigEndian( out ushort size ) || size > reader.Remaining )
            {
                output = ReadOnlySequence<byte>.Empty;
                return false;
            }
            output = reader.Sequence.Slice( reader.Position );
            return true;
        }


    }
}
