using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
        public static bool TryReadBigEndian( this ref SequenceReader<byte> sequenceReader, out ushort value )
        {
            bool status = sequenceReader.TryReadBigEndian( out short toCast );
            value = (ushort)toCast;
            return status;
        }

        /// <summary>
        /// Copy&Paste of https://github.com/dotnet/runtime/issues/29318#issuecomment-484987895
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="length"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool TryReadUtf8String(
    ref this SequenceReader<byte> reader, int length, out string? value )
        {
            ReadOnlySpan<byte> span = reader.UnreadSpan;
            if( span.Length < length )
                return TryReadMultisegmentUtf8String( ref reader, length, out value );

            ReadOnlySpan<byte> slice = span.Slice( 0, length );
            value = Encoding.UTF8.GetString( slice );
            reader.Advance( length );
            return true;
        }

        private static bool TryReadMultisegmentUtf8String(
            ref SequenceReader<byte> reader, int length, out string? value )
        {
            Debug.Assert( reader.UnreadSpan.Length < length );

            // Not enough data in the current segment, try to peek for the data we need.
            // In my use case, these strings cannot be more than 64kb, so stack memory is fine.
            Span<byte> buffer = stackalloc byte[length];
            if( !reader.TryCopyTo( buffer ) )
            {
                value = null;
                return false;
            }
            value = Encoding.UTF8.GetString( buffer );
            reader.Advance( length );
            return true;
        }
    }
}
