using CK.Core;
using CK.MQTT.Client.Deserialization;
using CK.MQTT.Client.OutgoingPackets;
using CK.MQTT.Common;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Serialisation;
using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Processes
{
    static class ConnectProcess
    {
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
                ConnectResult? connect = default;
                input.CurrentReflex = async ( m, header, size, reader ) =>
                {
                    int headerPartSize = pConf.ProtocolName.MQTTSize() + 4;
                    OperationStatus result = OperationStatus.NeedMoreData;
                    StartParse://We can avoid the goto, at the price of needing to work around the while scope, and variable declaration.
                    ReadResult read = await reader.ReadAsync();
                    if( read.IsCanceled ) return;
                    result = ConnectAck.Deserialize( m, read.Buffer, out byte state, out byte code, out int lenght, out SequencePosition position );
                    if( result == OperationStatus.NeedMoreData )
                    {
                        //if the stream was completed, there is no point to restart reading.
                        if( read.IsCompleted ) throw e = new EndOfStreamException();
                        //Here, we mark we observed the data, the next PipeReader.ReadAsync will return with more data.
                        reader.AdvanceTo( read.Buffer.Start, position );
                        goto StartParse;
                    }
                    if( result != OperationStatus.Done ) throw e = new ProtocolViolationException();
                    input.CurrentReflex = reflexPostConnect;
                    waitAck.Release();
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
