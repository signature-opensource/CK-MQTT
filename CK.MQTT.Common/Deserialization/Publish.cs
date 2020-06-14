using CK.MQTT.Common.Serialisation;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace CK.MQTT.Client.Deserialization
{
    static class Publish
    {
        internal static OperationStatus ParsePublishWithPacketId( ReadOnlySequence<byte> buffer, out string? topic, out ushort packetId )
        {
            SequenceReader<byte> reader = new SequenceReader<byte>( buffer );
            if( !reader.TryReadMQTTString( out topic ) )
            {
                packetId = 0;
                return OperationStatus.NeedMoreData;
            }
            if( !reader.TryReadBigEndian( out packetId ) ) return OperationStatus.NeedMoreData;
            return OperationStatus.Done;
        }
    }
}
