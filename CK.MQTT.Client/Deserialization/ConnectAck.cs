using CK.Core;
using System;
using System.Buffers;
using System.Diagnostics;

namespace CK.MQTT.Client.Deserialization
{
    static class ConnectAck
    {
        internal static void Deserialize( IActivityMonitor m, ReadOnlySequence<byte> buffer, out byte state, out byte code, out SequencePosition position )
        {
            SequenceReader<byte> reader = new SequenceReader<byte>( buffer );
            bool res = reader.TryRead( out state );
            bool res2 = reader.TryRead( out code );
            position = reader.Position;
            Debug.Assert( res && res2 );
        }
    }
}
