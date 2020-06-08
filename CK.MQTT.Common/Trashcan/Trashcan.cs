using System;
using System.Collections.Generic;
using System.Text;

namespace CK.MQTT.Common
{
    class Trashcan
    {
        public static OutgoingConnectAck? Deserialize( IActivityMonitor m, ReadOnlySequence<byte> buffer )
        {
            const string notEnoughBytes = "Malformed packet: Not enough bytes in the Connect packet.";
            if( buffer.Length < 2 )
            {
                m.Error( "Malformed Packet: Not enoughs bytes in the ConnectAck packet." );
                return null;
            }
            SequenceReader<byte> reader = new SequenceReader<byte>( buffer );
            if( !reader.TryRead( out byte session )
             || !reader.TryRead( out byte code ) )
            {
                m.Error( notEnoughBytes );
                return null;
            }
            OutgoingConnectAck output = new OutgoingConnectAck( (SessionState)session, (ConnectReturnCode)code );
            if( reader.Remaining > 0 )
            {
                m.Warn( "Malformed Packet: Didn't read all the bytes in the ConnectAck packet." );
            }
            return output;
        }

    }
}
