using System;
using System.Diagnostics;
using System.Text;

namespace CK.MQTT.Common.Serialisation
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

        public static Memory<byte> WriteRemainingLength( this Memory<byte> memory, uint remainingSize )
        {
            Debug.Assert( memory.Length > 0 );
            Span<byte> buffer = memory.Span;
            // Write out an int 7 bits at a time.  The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            uint v = remainingSize;   // support negative numbers
            int i = 0;
            while( v >= 0x80 )
            {
                buffer[i++] = (byte)(v | 0x80);
                v >>= 7;
            }
            buffer[i] = (byte)v;
            return memory[++i..];
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

        public static void WriteUInt16( this Span<byte> buffer, ushort x )
        {
            buffer[0] = (byte)(x >> 8);
            buffer[1] = (byte)x;
        }
    }
}
