using CK.Core;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CK.MQTT.Common.Serialisation
{
    static class MqttBinaryReader
    {
        public static ReadOnlySpan<byte> ReadString( this ReadOnlySpan<byte> buffer, out string? str )
        {
            ushort size = ReadUInt16( buffer );
            if( size > 65535 || size > buffer.Length )
            {
                str = null;
                return buffer[2..];
            }
            str = Encoding.UTF8.GetString( buffer[2..size] );
            return buffer[(2 + size)..];
        }

        public static ReadOnlyMemory<byte> ConditionallyReadString( this ReadOnlyMemory<byte> memory, out string? str, bool condition )
        {
            if( condition ) return ReadString( memory, out str );
            str = null;
            return memory;
        }

        /// <summary>
        /// Buffer must contain packet type.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="n">The Remaining Length, -1 if there is no enough bytes to read, -2 if the stream is corrupted.</param>
        /// <returns></returns>
        public static byte ReadRemainingLength( this ReadOnlySpan<byte> buffer, out int n )
        {
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            n = 0;
            int shift = 0;
            byte i = 0;
            byte b;
            int bufferLength = buffer.Length;
            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                if( shift == 5 * 7 )  // 5 bytes max per Int32, shift += 7
                {
                    n = -2;
                    return i;
                }
                if( i == bufferLength )
                {
                    n = -1;
                    return i;
                }
                // ReadByte handles end of stream cases for us.
                b = buffer[i];
                i++;
                n |= (b & 0x7F) << shift;
                shift += 7;
            } while( (b & 0x80) != 0 );
            return i;
        }


        public static ReadOnlyMemory<byte> ReadString( this ReadOnlyMemory<byte> memory, out string? str )
        {
            ReadOnlySpan<byte> buffer = memory.Span;
            ushort size = ReadUInt16( buffer );
            if( memory.Length < size )
            {
                str = null;
                return memory[2..];
            }
            str = Encoding.UTF8.GetString( buffer[2..size] );
            return memory[(2 + size)..];
        }

        public static ReadOnlyMemory<byte> ReadPayload( this ReadOnlyMemory<byte> memory, out ReadOnlyMemory<byte>? payload )
        {
            ReadOnlySpan<byte> buffer = memory.Span;
            ushort size = ReadUInt16( buffer );
            if( size > buffer.Length )
            {
                payload = null;
                return memory[2..];
            }
            payload = memory[2..size];
            return memory[(size + 2)..];
        }

        public static ushort ReadUInt16( this ReadOnlySpan<byte> buffer )
            => (ushort)((buffer[0] << 8) | buffer[1]);

        public static ReadOnlySpan<byte> ReadUInt16( this ReadOnlySpan<byte> buffer, out ushort uint16 )
        {
            uint16 = (ushort)((buffer[0] << 8) | buffer[1]);
            return buffer[2..];
        }
    }
}
