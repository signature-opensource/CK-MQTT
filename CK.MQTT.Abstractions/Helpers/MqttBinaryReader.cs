using CK.MQTT.Client;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Various extensions methods that help reading MQTT values on <see cref="PipeReader"/>, <see cref="SequenceReader{T}"/>, or <see cref="ReadOnlySequence{T}"/>.
    /// </summary>
    public static class MqttBinaryReader
    {
        /// <summary>
        /// Read the Remaining Length of a packet, <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_Toc304802782">as described in the spec</a>.
        /// </summary>
        /// <param name="reader">The reader to read the bytes from.</param>
        /// <param name="length">The Remaining Length, -1 if there is no enough bytes to read, -2 if the stream is corrupted.</param>
        /// <param name="position">The position after the remaining length.</param>
        /// <returns>An <see cref="OperationStatus"/>.</returns>
        public static OperationStatus TryReadMQTTRemainingLength( this ref SequenceReader<byte> reader, out uint length, out SequencePosition position )
        {
            length = 0;
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            int shift = 0;
            byte b;
            do
            {
                if( shift == 5 * 7 )// Check for a corrupted stream.  Read a max of 5 bytes.
                {
                    position = reader.Position;
                    return OperationStatus.InvalidData; // 5 bytes max per Int32, shift += 7
                }
                if( !reader.TryRead( out b ) ) // ReadByte handles end of stream cases for us.
                {
                    position = reader.Position;
                    return OperationStatus.NeedMoreData;
                }
                length |= (b & 0x7Fu) << shift;
                shift += 7;
            } while( (b & 0x80) != 0 );
            position = reader.Position;
            return OperationStatus.Done;
        }

        /// <summary>
        /// Try to read a mqtt string as <a href="docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_UTF-8_encoded_strings_">defined in the spec</a>.
        /// </summary>
        /// <param name="reader">The string will be read from this <see cref="SequenceReader{T}"/>.</param>
        /// <param name="output">The parsed string.</param>
        /// <returns><see langword="true"/> if the <see cref="string"/> was correctly read, <see langword="false"/> if there is not enough data.</returns>
        public static bool TryReadMQTTString( this ref SequenceReader<byte> reader, [NotNullWhen( true )] out string? output ) // TODO: this need to be used only once, to expose the topic to the user.
        {
            if( reader.TryReadBigEndian( out ushort size ) )
            {
                return reader.TryReadUtf8String( size, out output );
            }
            output = null;
            return false;
        }

        public static bool TryReadMQTTBinaryData( this ref SequenceReader<byte> reader, out ReadOnlyMemory<byte> memory )
        {
            if( !reader.TryReadBigEndian( out ushort length ) )
            {
                memory = ReadOnlyMemory<byte>.Empty;
                return false;
            }
            Memory<byte> buffer = new byte[length];
            memory = buffer;
            if( !reader.TryCopyTo( buffer.Span ) )
            {
                reader.Rewind( 2 );
                return false;
            }
            return true;
        }

        /// <summary>
        /// Skip bytes on a <see cref="PipeReader"/>, useful when you know the length of some data, but don't care about the content.
        /// </summary>
        /// <param name="reader">The <see cref="PipeReader"/> to use.</param>
        /// <param name="skipCount">The number of <see cref="byte"/> to skip.</param>
        /// <returns>The awaitable.</returns>
        public static async ValueTask SkipBytesAsync( this PipeReader reader, IMqtt3Sink sink, ushort packetId, uint skipCount, CancellationToken cancellationToken )
        {
            ReadResult read = await reader.ReadAtLeastAsync( (int)skipCount, cancellationToken );
            sink.OnUnparsedExtraData( packetId, read.Buffer.Slice( 0, read.Buffer.Length > skipCount ? skipCount : read.Buffer.Length ) );
            reader.AdvanceTo( read.Buffer.Slice( skipCount ).Start );
            // We mark the data as consumed.
            reader.AdvanceTo( read.Buffer.End );
        }
    }
}
