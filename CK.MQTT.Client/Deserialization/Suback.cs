using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CK.MQTT.Abstractions.Serialisation;

namespace CK.MQTT.Client.Deserialization
{
    class Suback
    {
        public static bool TryParse( ReadOnlySequence<byte> buffer,
            int payloadLength,
            out ushort packetId,
            [NotNullWhen( true )] out QualityOfService[]? qos,
            out SequencePosition position )
        {
            SequenceReader<byte> reader = new SequenceReader<byte>( buffer );
            if( !reader.TryReadBigEndian( out packetId ) )
            {
                qos = null;
                position = reader.Position;
                return false;
            }
            buffer = buffer.Slice( 2, payloadLength - 2 );
            qos = (QualityOfService[])(object)buffer.ToArray();
            position = buffer.End;
            return true;
        }
    }
}
