using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using CK.MQTT.Abstractions.Serialisation;

namespace CK.MQTT.Common.Deserialization
{
    public static class Publish
    {
        public static bool ParsePublishWithPacketId(
            ReadOnlySequence<byte> buffer,
            [NotNullWhen( true )] out string? topic, out ushort packetId, out SequencePosition position )
        {
            SequenceReader<byte> reader = new SequenceReader<byte>( buffer );
            if( !reader.TryReadMQTTString( out topic ) )
            {
                packetId = 0;
                position = reader.Position;
                return false;
            }
            if( !reader.TryReadBigEndian( out packetId ) )
            {
                position = reader.Position;
                return false;
            }
            position = reader.Position;
            return true;
        }
    }
}
