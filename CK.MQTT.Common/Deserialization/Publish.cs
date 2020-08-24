using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace CK.MQTT
{
    /// <summary>
    /// Helper to parse Publish packets.
    /// </summary>
    public static class Publish
    {
        /// <summary>
        /// Parse a publish packet with a packet id.
        /// Simply read the topic, packet id, and give their results by out parameters.
        /// </summary>
        /// <param name="buffer">The buffer to read the data from.</param>
        /// <param name="topic">The topic of the publish packet.</param>
        /// <param name="packetId">The packet id of the publish packet.</param>
        /// <param name="position">The position after the read data.</param>
        /// <returns>true if there was enough data, false if more data is required.</returns>
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
