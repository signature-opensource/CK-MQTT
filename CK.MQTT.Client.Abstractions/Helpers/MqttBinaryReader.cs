using CK.Core;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public static class MqttBinaryReader
    {
        /// <summary>
        /// Buffer must contain packet type.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="length">The Remaining Length, -1 if there is no enough bytes to read, -2 if the stream is corrupted.</param>
        /// <returns></returns>
        public static OperationStatus TryReadMQTTRemainingLength( this ref SequenceReader<byte> reader, out int length, out SequencePosition position )
        {
            length = 0;
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            int shift = 0;
            byte b;
            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                if( shift == 5 * 7 )
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
#pragma warning disable CS8762 // Parameter must have a non-null value when exiting in some condition.
            return result; // https://github.com/dotnet/roslyn/issues/44080
#pragma warning restore CS8762 // Parameter must have a non-null value when exiting in some condition.
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

        static bool TryReadUInt16( ReadOnlySequence<byte> buffer, out ushort val, out SequencePosition sequencePosition )
        {
            SequenceReader<byte> reader = new SequenceReader<byte>( buffer );
            bool result = reader.TryReadBigEndian( out val );
            sequencePosition = reader.Position;
            return result;
        }

        public static async ValueTask BurnBytes( this PipeReader reader, int byteCountToBurn )
        {
            while( byteCountToBurn > 0 ) //"simply" burn the bytes of the packet.
            {

                ReadResult read = await reader.ReadAsync();
                int bufferLength = (int)read.Buffer.Length;
                if( bufferLength > byteCountToBurn )
                {
                    reader.AdvanceTo( read.Buffer.Slice( bufferLength ).Start );
                    break;
                }
                reader.AdvanceTo( read.Buffer.End );
                byteCountToBurn -= bufferLength;
            };
        }

        public static async ValueTask<ReadResult?> ReadAsync( this PipeReader pipeReader, IMqttLogger m, int minimumByteCount, CancellationToken cancellationToken = default )
        {
            while( true )
            {
                ReadResult result = await pipeReader.ReadAsync( cancellationToken );
                if( result.Buffer.Length >= minimumByteCount ) return result;
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
                pipeReader.AdvanceTo( result.Buffer.Start, result.Buffer.End );
            }
        }

        /// <summary>
        /// Don't use this if you have to parse multiples fields in a row.
        /// </summary>
        /// <param name="pipeReader"></param>
        /// <returns></returns>
        public static async ValueTask<ushort> ReadPacketIdPacket( this PipeReader pipeReader, IMqttLogger m, int packetSize )
        {
            while( true )
            {
                ReadResult result = await pipeReader.ReadAsync();
                if( result.IsCanceled ) throw new OperationCanceledException();
                if( TryReadUInt16( result.Buffer, out ushort output, out SequencePosition sequencePosition ) )
                {
                    pipeReader.AdvanceTo( sequencePosition );
                    packetSize -= 2;
                    if( packetSize > 0 )
                    {
                        m.Warn( $"Packet bigger than expected, skipping {packetSize} bytes." );
                        await pipeReader.BurnBytes( packetSize );
                    }
                    return output;
                }
                pipeReader.AdvanceTo( result.Buffer.Start, result.Buffer.End );//We are really not lucky, we needed only TWO bytes.
                if( result.IsCompleted ) throw new EndOfStreamException();
                continue;
            }
        }

        public static bool TryReadMQTTPayload( this ref SequenceReader<byte> reader, out ReadOnlySequence<byte> output )
        {
            bool failed = !reader.TryReadBigEndian( out ushort size ) || size > reader.Remaining;
            output = reader.Sequence.Slice( reader.Position );
            return failed;
        }


    }
}
