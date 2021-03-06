using System;
using System.Diagnostics;
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

        /// <summary>
        /// Write a mqtt string.
        /// </summary>
        /// <param name="buffer">The buffer to write in.</param>
        /// <param name="str">The string to write.</param>
        /// <returns>The <paramref name="buffer"/> but sliced after the writtens bytes (2+ the number of bytes in the string).</returns>
        public static Span<byte> WriteMQTTString( this Span<byte> buffer, string str )
        {
            Debug.Assert( str.Length <= ushort.MaxValue );
            WriteBigEndianUInt16( buffer, (ushort)str.Length );//Write the string length.
            buffer = buffer[2..];//slice out the uint16.
            if( str.Length == 0 ) return buffer;//if the string is empty, simply return the remaining buffer.
            int copyAmount = Encoding.UTF8.GetBytes( str.AsSpan(), buffer );//mqtt string are utf8. http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html#_UTF-8_encoded_strings_
            return buffer[copyAmount..];//slice out what we written.
        }

        /// <summary>
        /// Write an <see cref="ushort"/>.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="x">The <see cref="ushort"/> to write.</param>
        /// <returns>The <paramref name="buffer"/>  but sliced after the 2 writtens bytes.</returns>
        public static Span<byte> WriteBigEndianUInt16( this Span<byte> buffer, ushort x )
        {
            buffer[0] = (byte)(x >> 8);
            buffer[1] = (byte)(x >> 0);
            return buffer[2..];
        }

        public static Span<byte> WriteBigEndianUInt32( this Span<byte> buffer, uint x )
        {
            buffer[0] = (byte)(x >> 24);
            buffer[1] = (byte)(x >> 16);
            buffer[2] = (byte)(x >> 8);
            buffer[3] = (byte)(x >> 0);
            return buffer[4..];
        }

        public static Span<byte> WriteBinaryData( this Span<byte> span, ReadOnlyMemory<byte> memory )
        {
            if( memory.Length > ushort.MaxValue ) throw new ArgumentException( $"Binary Data size should not exceed {ushort.MaxValue} bytes." );
            span = span.WriteBigEndianUInt16( (ushort)memory.Length );
            memory.Span.CopyTo( span );
            return span[memory.Length..];
        }
    }
}
