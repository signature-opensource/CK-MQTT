using System;
using System.Buffers.Binary;
using System.Text;

namespace CK.MQTT
{
    /// <summary>
    /// Various extension methods that help serializing MQTT values on a <see cref="Span{T}"/>.
    /// </summary>
    public static class MqttBinaryWriter
    {
        /// <summary>
        /// Write the packet length.
        /// </summary>
        /// <param name="buffer">The buffer to write in.</param>
        /// <param name="packetLength">The length of the packet to write.</param>
        /// <returns>The <paramref name="buffer"/> but sliced after the writtens bytes (at least 1 byte, up to 5 bytes).</returns>
        public static Span<byte> WriteVariableByteInteger( this Span<byte> buffer, int packetLength )
        {
            // Write out an int 7 bits at a time.  The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            int i = 0;
            while( packetLength >= 0x80 )
            {
                buffer[i++] = (byte)(packetLength | 0x80);
                packetLength >>= 7;
            }
            buffer[i] = (byte)packetLength;
            return buffer[(i + 1)..];
        }

        static readonly Range[] _invalidRanges = { new( 0x0001, 0x001F ), new( 0xD800, 0xDFFF ), new( 0x0000, 0x0000 ), new( 0x007F, 0x009F ) };

        static bool IsInvalidString( string str )
        {
            foreach( char c in str )
            {
                foreach( var range in _invalidRanges )
                {
                    int cInt = c;
                    if( cInt >= range.Start.Value && cInt <= range.End.Value ) return true;
                }
            }
            return false;
        }

        static bool StringTooLong( string str ) => str.Length > ushort.MaxValue;

        public static void ThrowIfInvalidMQTTString( string str )
        {
            if( StringTooLong( str ) ) throw new ArgumentException( "Serializing a string that is longer than 65535 chars." );
            if( IsInvalidString( str ) ) throw new ArgumentException( "The string contain invalid chars." );
        }

        /// <summary>
        /// Write a mqtt string.
        /// </summary>
        /// <param name="buffer">The buffer to write in.</param>
        /// <param name="str">The string to write.</param>
        /// <returns>The <paramref name="buffer"/> but sliced after the writtens bytes (2+ the number of bytes in the string).</returns>
        public static Span<byte> WriteMQTTString( this Span<byte> buffer, string str )
        {
            ThrowIfInvalidMQTTString( str );
            BinaryPrimitives.WriteUInt16BigEndian( buffer, (ushort)str.Length ); //Write the string length.
            buffer = buffer[2..];//slice out the uint16.
            if( str.Length == 0 ) return buffer;//if the string is empty, simply return the remaining buffer.
            int copyAmount = Encoding.UTF8.GetBytes( str.AsSpan(), buffer );//mqtt string are utf8. http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_UTF-8_encoded_strings_
            return buffer[copyAmount..];//slice out what we written.
        }

        public static Span<byte> WriteBinaryData( this Span<byte> span, ReadOnlyMemory<byte> memory )
        {
            if( memory.Length > ushort.MaxValue ) throw new ArgumentException( $"Binary Data size should not exceed {ushort.MaxValue} bytes." );
            BinaryPrimitives.WriteUInt16BigEndian( span, (ushort)memory.Length );
            memory.Span.CopyTo( span.Slice( 4 ) );
            return span[memory.Length..];
        }
    }
}
