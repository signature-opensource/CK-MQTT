using CK.Core;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        

        


        public static string? ReadString( this ref SequenceParser<byte> reader )
        {
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
