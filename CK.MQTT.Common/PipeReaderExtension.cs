using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public static class CKPipeReaderExtensions
    {
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
            SequenceReader<byte> reader = new( buffer );
            bool result = reader.TryReadBigEndian( out val );
            sequencePosition = reader.Position;
            return result;
        }

        /// <summary>
        /// Read a <see cref="ushort"/> directly from a <see cref="PipeReader"/>. Use this only if you are in an async context, and the next read cannot use a <see cref="SequenceReader{T}"/>.
        /// </summary>
        /// <param name="pipeReader">The <see cref="PipeReader"/> to read the data from.</param>
        /// <param name="m">The <see cref="IMqttLogger"/> to use.</param>
        /// <param name="remainingLength">The remaining length of the packet. If it's bigger than 2, will log a warning.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that contain a <see cref="ushort"/> when completed.</returns>
        public static async ValueTask<ushort?> ReadPacketIdPacketAsync( this PipeReader pipeReader, IInputLogger? m, int remainingLength )
        {
            while( true )//If the data was not available on the first try, we redo the process.
            {
                ReadResult result = await pipeReader.ReadAsync();
                if( result.IsCanceled ) return null;
                if( TryReadUInt16( result.Buffer, out ushort output, out SequencePosition sequencePosition ) )
                { //ushort was correctly read.
                    pipeReader.AdvanceTo( sequencePosition );//we mark that the data was read.
                    remainingLength -= 2;
                    if( remainingLength > 0 ) //The packet may contain more data, but we don't know how to process it, so we skip it.
                    {

                        m?.UnparsedExtraBytesPacketId( remainingLength );
                        await pipeReader.SkipBytesAsync( remainingLength );
                    }
                    return output;
                }
                //We mark that all the data was observed, the next Read operation won't complete until more data are available.
                pipeReader.AdvanceTo( result.Buffer.Start, result.Buffer.End );//We are really not lucky, we needed only TWO bytes.
                if( result.IsCompleted ) return null;
            }
        }
    }
}
