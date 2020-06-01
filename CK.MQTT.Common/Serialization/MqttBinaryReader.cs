using CK.Core;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Serialisation
{


    static class MqttBinaryReader
    {
        public static bool TryReadMQTTString( this ref SequenceReader<byte> reader, [NotNullWhen( true )] out string? output )
        {
            ushort size = reader.ReadUInt16();
            if( size > reader.Remaining )
            {
                output = null;
                return false;
            }
            output = reader.ReadString( size, Encoding.UTF8 );
            return true;
        }

        public static bool TryReadMQTTPayload( this ref SequenceReader<byte> reader, out ReadOnlySequence<byte> output )
        {
            ushort size = reader.ReadUInt16();
            if( size > reader.Remaining )
            {
                output = ReadOnlySequence<byte>.Empty;
                return false;
            }
            output = reader.Sequence.Slice( reader.Position );
            return true;
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
