// Modified code from https://github.com/dotnet/runtime/blob/4f9ae42d861fcb4be2fcd5d3d55d5f227d30e723/src/libraries/System.Memory/src/System/Buffers/SequenceParserExtensions.Binary.cs
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CK.MQTT.Serialization
{
    public static partial class SequenceParserExtensions
    {
        /// <summary>
        /// Try to read the given type out of the buffer if possible. Warning: this is dangerous to use with arbitrary
        /// structs- see remarks for full details.
        /// </summary>
        /// <remarks>
        /// IMPORTANT: The read is a straight copy of bits. If a struct depends on specific state of it's members to
        /// behave correctly this can lead to exceptions, etc. If reading endian specific integers, use the explicit
        /// overloads such as <see cref="TryReadLittleEndian(ref SequenceParser{byte}, out short)"/>
        /// </remarks>
        /// <returns>
        /// True if successful. <paramref name="value"/> will be default if failed (due to lack of space).
        /// </returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal static unsafe bool TryRead<T>( ref this SequenceParser<byte> reader, out T value ) where T : unmanaged
        {
            ReadOnlySpan<byte> span = reader.UnreadSpan;
            if( span.Length < sizeof( T ) )
                return TryReadMultisegment( ref reader, out value );

            value = Unsafe.ReadUnaligned<T>( ref MemoryMarshal.GetReference( span ) );
            reader.Advance( sizeof( T ) );
            return true;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal static unsafe T Read<T>( ref this SequenceParser<byte> reader ) where T : unmanaged
        {
            ReadOnlySpan<byte> span = reader.UnreadSpan;
            if( span.Length < sizeof( T ) ) return ReadMultisegment( ref reader );

            T value = Unsafe.ReadUnaligned<T>( ref MemoryMarshal.GetReference( span ) );
            reader.Advance( sizeof( T ) );
            return value;
        }

        private static unsafe bool TryReadMultisegment<T>( ref SequenceParser<byte> reader, out T value ) where T : unmanaged
        {
            Debug.Assert( reader.UnreadSpan.Length < sizeof( T ) );

            // Not enough data in the current segment, try to peek for the data we need.
            T buffer = default;
            Span<byte> tempSpan = new Span<byte>( &buffer, sizeof( T ) );

            if( !reader.TryCopyTo( tempSpan ) )
            {
                value = default;
                return false;
            }

            value = Unsafe.ReadUnaligned<T>( ref MemoryMarshal.GetReference( tempSpan ) );
            reader.Advance( sizeof( T ) );
            return true;
        }

        private static T ReadMultisegment<T>( ref SequenceParser<byte> reader  ) where T : unmanaged
        {
            if( !TryReadMultisegment( ref reader, out T value ) ) reader.SetError();
            return value;
        }

        /// <summary>
        /// Reads an <see cref="short"/> as little endian.
        /// </summary>
        /// <returns>False if there wasn't enough data for an <see cref="short"/>.</returns>
        public static bool TryReadLittleEndian( ref this SequenceParser<byte> reader, out short value )
        {
            if( BitConverter.IsLittleEndian )
            {
                return reader.TryRead( out value );
            }

            return TryReadReverseEndianness( ref reader, out value );
        }



        /// <summary>
        /// Reads an <see cref="ushort"/> as little endian.
        /// </summary>
        /// <returns>False if there wasn't enough data for an <see cref="short"/>.</returns>
        public static bool TryReadLittleEndian( ref this SequenceParser<byte> reader, out ushort value )
        {
            if( BitConverter.IsLittleEndian )
            {
                return reader.TryRead( out value );
            }

            return TryReadReverseEndianness( ref reader, out value );
        }


        /// <summary>
        /// Reads an <see cref="short"/> as big endian.
        /// </summary>
        /// <returns>False if there wasn't enough data for an <see cref="short"/>.</returns>
        public static bool TryReadBigEndian( ref this SequenceParser<byte> reader, out short value )
        {
            if( !BitConverter.IsLittleEndian )
            {
                return reader.TryRead( out value );
            }

            return TryReadReverseEndianness( ref reader, out value );
        }

        private static bool TryReadReverseEndianness( ref SequenceParser<byte> reader, out short value )
        {
            if( reader.TryRead( out value ) )
            {
                value = BinaryPrimitives.ReverseEndianness( value );
                return true;
            }

            return false;
        }

        private static bool TryReadReverseEndianness( ref SequenceParser<byte> reader, out ushort value )
        {
            if( reader.TryRead( out value ) )
            {
                value = BinaryPrimitives.ReverseEndianness( value );
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reads an <see cref="int"/> as little endian.
        /// </summary>
        /// <returns>False if there wasn't enough data for an <see cref="int"/>.</returns>
        public static bool TryReadLittleEndian( ref this SequenceParser<byte> reader, out int value )
        {
            if( BitConverter.IsLittleEndian )
            {
                return reader.TryRead( out value );
            }

            return TryReadReverseEndianness( ref reader, out value );
        }

        /// <summary>
        /// Reads an <see cref="int"/> as big endian.
        /// </summary>
        /// <returns>False if there wasn't enough data for an <see cref="int"/>.</returns>
        public static bool TryReadBigEndian( ref this SequenceParser<byte> reader, out int value )
        {
            if( !BitConverter.IsLittleEndian )
            {
                return reader.TryRead( out value );
            }

            return TryReadReverseEndianness( ref reader, out value );
        }

        private static bool TryReadReverseEndianness( ref SequenceParser<byte> reader, out int value )
        {
            if( reader.TryRead( out value ) )
            {
                value = BinaryPrimitives.ReverseEndianness( value );
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reads an <see cref="long"/> as little endian.
        /// </summary>
        /// <returns>False if there wasn't enough data for an <see cref="long"/>.</returns>
        public static bool TryReadLittleEndian( ref this SequenceParser<byte> reader, out long value )
        {
            if( BitConverter.IsLittleEndian )
            {
                return reader.TryRead( out value );
            }

            return TryReadReverseEndianness( ref reader, out value );
        }

        /// <summary>
        /// Reads an <see cref="long"/> as big endian.
        /// </summary>
        /// <returns>False if there wasn't enough data for an <see cref="long"/>.</returns>
        public static bool TryReadBigEndian( ref this SequenceParser<byte> reader, out long value )
        {
            if( !BitConverter.IsLittleEndian )
            {
                return reader.TryRead( out value );
            }

            return TryReadReverseEndianness( ref reader, out value );
        }

        private static bool TryReadReverseEndianness( ref SequenceParser<byte> reader, out long value )
        {
            if( reader.TryRead( out value ) )
            {
                value = BinaryPrimitives.ReverseEndianness( value );
                return true;
            }

            return false;
        }
    }
}
