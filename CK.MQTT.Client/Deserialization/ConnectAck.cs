using CK.Core;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using System;
using System.Buffers;
using System.Diagnostics;

namespace CK.MQTT.Client.Deserialization
{
    static class ConnectAck
    {
        internal static OperationStatus Deserialize(
            IActivityMonitor m, ReadOnlySequence<byte> buffer,
            out byte state, out byte code, out int length, out SequencePosition position )
        {
            Debug.Assert( buffer.Length > 3 );
            SequenceReader<byte> reader = new SequenceReader<byte>( buffer );
            reader.TryRead( out byte packetType );
            if( ((PacketType)packetType) != PacketType.ConnectAck )
            {
                length = state = code = 0;
                position = reader.Position;
                return OperationStatus.InvalidData;
            }
            OperationStatus result = reader.TryReadMQTTRemainingLength( out length );
            if( result != OperationStatus.Done )
            {
                state = code = 0;
                position = reader.Position;
                return result;
            }
            if( !reader.TryRead( out state ) || !reader.TryRead( out code ) )
            {
                code = 0;
                position = reader.Position;
                return OperationStatus.NeedMoreData;
            }
            if( reader.Remaining > 0 ) m.Warn( "Unread data in Connect packet." );
            position = reader.Position;
            return OperationStatus.Done;
        }
    }
}
