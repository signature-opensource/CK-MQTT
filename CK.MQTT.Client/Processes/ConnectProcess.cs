using CK.Core;
using CK.MQTT.Client.OutgoingPackets;
using CK.MQTT.Common;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using CK.MQTT.Common.Serialisation;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static CK.MQTT.Common.Channels.IncomingMessageHandler;

namespace CK.MQTT.Client.Processes
{
    static class ConnectProcess
    {
        static SequenceReadResult Deserialize( IActivityMonitor m, ReadOnlySequence<byte> buffer, out byte state, out byte code, out int length )
        {
            Debug.Assert( buffer.Length > 3 );
            SequenceReader<byte> reader = new SequenceReader<byte>( buffer );
            reader.TryRead( out byte packetType );
            if( ((PacketType)packetType) != PacketType.ConnectAck )
            {
                length = state = code = 0;
                return SequenceReadResult.CorruptedStream;
            }
            SequenceReadResult result = reader.TryReadMQTTRemainingLength( out length );
            if( result != SequenceReadResult.Ok )
            {
                state = code = 0;
                return result;
            }
            if( !reader.TryRead( out state ) || !reader.TryRead( out code ) )
            {
                code = 0;
                return SequenceReadResult.NotEnoughBytes;
            }
            if( reader.Remaining > 0 ) m.Warn( "Unread data in Connect packet." );
            return SequenceReadResult.Ok;
        }

        public static async Task<ConnectResult> ExecuteConnectProtocol(
            IActivityMonitor m,
            OutgoingMessageHandler output,
            IncomingMessageHandler input,
            OutgoingConnect outgoingConnect,
            ProtocolConfiguration pConf,
            int waitTimeoutSecs,
            Reflex reflexPostConnect )
        {
            using( m.OpenInfo( "Executing connect protocol..." ) )
            {
                Exception? e = null;
                SemaphoreSlim waitAck = new SemaphoreSlim( 0 );
                byte state;
                byte code;
                input.CurrentReflex = async ( m, header, size, reader ) =>
                {
                    int headerPartSize = pConf.ProtocolName.MQTTSize() + 4;
                    ReadResult read;
                    SequenceReadResult result = SequenceReadResult.NotEnoughBytes;
                    int lenght;
                    while( result == SequenceReadResult.NotEnoughBytes )
                    {
                        read = await reader.ReadAsync();
                        if( read.Buffer.Length < 4 ) read = await reader.ReadAsync();
                        if( read.IsCanceled ) return;
                        if( read.IsCompleted ) throw e = new EndOfStreamException();
                        result = Deserialize( m, read.Buffer, out state, out code, out lenght );
                    }
                    reader.Advance
                    if( result != SequenceReadResult.Ok ) throw new ProtocolViolationException();
                    input.CurrentReflex = reflexPostConnect;
                };

                output.QueueMessage( outgoingConnect, true ); // This is sent as a reflex, there shouldn't be any message in both queue.
                bool waitRes = await waitAck.WaitAsync( waitTimeoutSecs * 1000 );
                if( e != null ) throw e;
                if( !waitRes ) return new ConnectResult( SessionState.Error, ConnectReturnCode.ServerUnavailable );
                if( !connect.HasValue ) throw new InvalidOperationException();
                return connect.Value;
            }
        }
    }
}
