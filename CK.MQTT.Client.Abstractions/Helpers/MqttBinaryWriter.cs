using System;
using System.Diagnostics;
using System.Text;

namespace CK.MQTT
{
    public static class MqttBinaryWriter
    {
        public static byte ComputeRemainingLengthSize( uint remainingSize )
        {
            byte output = 1;
            while( remainingSize >= 0x80 )
            {
                output++;
                remainingSize >>= 7;
            }
            return output;
        }

        /// <summary>
        /// Write the byte header, then the packet length. Return the number of byte written.
        /// </summary>
        /// <param name="buffer">The buffer to write in.</param>
        /// <param name="header">The header to write.</param>
        /// <param name="packetLength">The length of the packet.</param>
        /// <returns></returns>
        public static Span<byte> WriteRemainingSize( this Span<byte> buffer, int packetLength )
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

        public static Span<byte> WriteString( this Span<byte> buffer, string str )
        {
            Debug.Assert( str.Length <= ushort.MaxValue );
            WriteUInt16( buffer, (ushort)str.Length );
            if( str.Length == 0 ) return buffer[2..];
            int copyAmount = Encoding.UTF8.GetBytes( str.AsSpan(), buffer[2..] );
            return buffer[(copyAmount + 2)..];
        }

        public static Span<byte> WritePayload( this Span<byte> buffer, ReadOnlySpan<byte> payload )
        {
            Debug.Assert( payload.Length <= ushort.MaxValue );
            WriteUInt16( buffer, (ushort)payload.Length );
            payload.CopyTo( buffer[2..] );
            return buffer[..(payload.Length + 2)];
        }

        public static Span<byte> WriteUInt16( this Span<byte> buffer, ushort x )
        {
            buffer[0] = (byte)(x >> 8);
            buffer[1] = (byte)x;
            return buffer[2..];
        }
    }
}
