using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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
        public static OperationStatus TryReadMQTTRemainingLength( this ref SequenceReader<byte> reader, out int length, out SequencePosition position )
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
                length |= (b & 0x7F) << shift;
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
        public static bool TryReadMQTTString( this ref SequenceReader<byte> reader, [NotNullWhen( true )] out string? output )
        {
            if( reader.TryReadBigEndian( out ushort size ) ) return reader.TryReadUtf8String( size, out output );
            output = null;
            return false;
        }

        /// <summary>
        /// Read a mqtt string on a <see cref="ReadOnlySequence{T}"/>, usefull when you cannot create a SequenceReader because you are on an async context.
        /// </summary>
        /// <param name="buffer">The buffer to read the string from.</param>
        /// <param name="output">The parsed <see cref="string"/>.</param>
        /// <param name="sequencePosition">The <see cref="SequencePosition"/> after the string.</param>
        /// <returns><see langword="true"/> if the <see cref="string"/> was correctly read, <see langword="false"/> if there is not enough data.</returns>
        static bool TryReadMQTTString( ReadOnlySequence<byte> buffer, [NotNullWhen( true )] out string? output, out SequencePosition sequencePosition )
        {
            SequenceReader<byte> reader = new SequenceReader<byte>( buffer );
            bool result = reader.TryReadMQTTString( out output );
            sequencePosition = reader.Position;
            Debug.Assert( result == (output != null) );
            return output != null;// TODO: https://github.com/dotnet/roslyn/issues/44080
        }

        /// <summary>
        /// Read a <see cref="ushort"/> on a <see cref="ReadOnlySequence{T}"/>,
        /// usefull when you cannot create a SequenceReader because you are on an async context.
        /// </summary>
        /// <param name="buffer">The buffer to read the string from.</param>
        /// <param name="val">The parsed <see cref="ushort"/>.</param>
        /// <param name="sequencePosition">The <see cref="SequenceReader{T}"/> after the <see cref="ushort"/>.</param>
        /// <returns></returns>
        static bool TryReadUInt16( ReadOnlySequence<byte> buffer, out ushort val, out SequencePosition sequencePosition )
        {
            SequenceReader<byte> reader = new SequenceReader<byte>( buffer );
            bool result = reader.TryReadBigEndian( out val );
            sequencePosition = reader.Position;
            return result;
        }

        /// <summary>
        /// Skip bytes on a <see cref="PipeReader"/>, usefull when you know the length of some data, but don't care about the content.
        /// </summary>
        /// <param name="reader">The <see cref="PipeReader"/> to use.</param>
        /// <param name="byteCountToBurn">The number of <see cref="byte"/> to burn.</param>
        /// <returns></returns>
        public static async ValueTask BurnBytes( this PipeReader reader, int byteCountToBurn )
        {
            while( byteCountToBurn > 0 ) //"simply" burn the bytes of the packet.
            {
                ReadResult read = await reader.ReadAsync();
                int bufferLength = (int)read.Buffer.Length;
                if( bufferLength > byteCountToBurn ) //if the read fetched more data than what we wanted to burn
                {
                    reader.AdvanceTo( read.Buffer.Slice( bufferLength ).Start );//We need to advance exactly the amount needed.
                    return;//and the job is done.
                }
                reader.AdvanceTo( read.Buffer.End );//we mark the data as consumed.
                byteCountToBurn -= bufferLength;
            };
        }

        /// <summary>
        /// Return a read with a buffer containing at least the given amount of bytes.
        /// Use this only with small number, it's made to simplify functions that expect a few bytes.
        /// </summary>
        /// <param name="pipeReader">The <see cref="PipeReader"/> to read from.</param>
        /// <param name="m">The <see cref="IMqttLogger"/> to use.</param>
        /// <param name="minimumByteCount">The minimum amout of byte the buffer should contain when this method complete.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>, to cancel the read.</param>
        /// <returns>A buffer containing at least <paramref name="minimumByteCount"/> bytes.</returns>
        public static async ValueTask<ReadResult?> ReadAsync( this PipeReader pipeReader, IMqttLogger m, int minimumByteCount, CancellationToken cancellationToken = default )
        {
            while( true )
            {
                ReadResult result = await pipeReader.ReadAsync( cancellationToken );
                if( result.Buffer.Length >= minimumByteCount ) return result;//The read returned enough bytes.
                if( result.IsCanceled )
                {
                    m.Error( "Connect reading canceled." );
                    return null;
                }
                if( result.IsCompleted )
                {
                    m.Error( "Unexpected End Of Stream" );
                    return null;
                }
                //mark the data as observed, the next read wont return until more data are available.
                pipeReader.AdvanceTo( result.Buffer.Start, result.Buffer.End );
            }
        }

        /// <summary>
        /// Read a mqtt string directly from a <see cref="PipeReader"/>. Use this only if you are in an async context, and the next read cannot use a <see cref="SequenceReader{T}"/>.
        /// </summary>
        /// <param name="pipeReader">The <see cref="PipeReader"/> to read the data from.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that complete when the string is read.</returns>
        public static async ValueTask<string> ReadMQTTString( this PipeReader pipeReader )
        {
            while( true ) //If the data was not available on the first try, we redo the process.
            {
                ReadResult result = await pipeReader.ReadAsync();
                if( result.IsCanceled ) throw new OperationCanceledException();//The read may have been canceled.
                if( TryReadMQTTString( result.Buffer, out string? output, out SequencePosition sequencePosition ) )
                { //string was correctly read.
                    pipeReader.AdvanceTo( sequencePosition );//we mark that the data was read.
                    return output;
                }
                //We mark that all the data was observed, the next Read operation won't complete until more data are available.
                pipeReader.AdvanceTo( result.Buffer.Start, result.Buffer.End );
                if( result.IsCompleted ) throw new EndOfStreamException();//we may have hit an end of stream...
            }
        }

        /// <summary>
        /// Read a <see cref="ushort"/> directly from a <see cref="PipeReader"/>. Use this only if you are in an async context, and the next read cannot use a <see cref="SequenceReader{T}"/>.
        /// </summary>
        /// <param name="pipeReader">The <see cref="PipeReader"/> to read the data from.</param>
        /// <param name="m">The <see cref="IMqttLogger"/> to use.</param>
        /// <param name="remainingLength">The remaining length of the packet. If it's bigger than 2, will log a warning.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that contain a <see cref="ushort"/> when completed.</returns>
        public static async ValueTask<ushort> ReadPacketIdPacket( this PipeReader pipeReader, IMqttLogger m, int remainingLength )
        {
            while( true )//If the data was not available on the first try, we redo the process.
            {
                ReadResult result = await pipeReader.ReadAsync();
                if( result.IsCanceled ) throw new OperationCanceledException();//The read may have been canceled.
                if( TryReadUInt16( result.Buffer, out ushort output, out SequencePosition sequencePosition ) )
                { //ushort was correctly read.
                    pipeReader.AdvanceTo( sequencePosition );//we mark that the data was read.
                    remainingLength -= 2;
                    if( remainingLength > 0 ) //The packet may contain more data, but we don't know how to process it, so we skip it.
                    {
                        m.Warn( $"Packet bigger than expected, skipping {remainingLength} bytes." );
                        await pipeReader.BurnBytes( remainingLength );
                    }
                    return output;
                }
                //We mark that all the data was observed, the next Read operation won't complete until more data are available.
                pipeReader.AdvanceTo( result.Buffer.Start, result.Buffer.End );//We are really not lucky, we needed only TWO bytes.
                if( result.IsCompleted ) throw new EndOfStreamException();//we may have hit an end of stream...
            }
        }
    }
}