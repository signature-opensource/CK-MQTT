using CK.Core;
using CK.MQTT.Client.Deserialization;
using CK.MQTT.Common;
using CK.MQTT.Common.Channels;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Reflexes
{
    class ConnectAckReflex
    {
        readonly IncomingMessageHandler _input;
        readonly Reflex _reflexPostConnect;
        readonly SemaphoreSlim _waitAck;

        public ConnectAckReflex( IncomingMessageHandler input, Reflex reflexPostConnect, SemaphoreSlim waitAck )
        {
            _input = input;
            _reflexPostConnect = reflexPostConnect;
            _waitAck = waitAck;
        }

        public Exception? Exception { get; private set; }

        public async ValueTask ProcessIncomingPacket( IActivityMonitor m, byte header, int packetSize, PipeReader reader )
        {
            try
            {
                StartParse://We can avoid the goto, at the price of needing to work around the while scope, and variable declaration.
                ReadResult read = await reader.ReadAsync();
                if( read.IsCanceled ) return;
                OperationStatus result = ConnectAck.Deserialize( m, read.Buffer, out byte state, out byte code, out int lenght, out SequencePosition position );
                if( result == OperationStatus.NeedMoreData )
                {
                    //if the stream was completed, there is no point to restart reading.
                    if( read.IsCompleted ) throw Exception = new EndOfStreamException();
                    //Here, we mark we observed the data, the next PipeReader.ReadAsync will return with more data.
                    reader.AdvanceTo( read.Buffer.Start, position );
                    goto StartParse;
                }
                reader.AdvanceTo( position );
                if( result != OperationStatus.Done ) throw Exception = new ProtocolViolationException();
                _input.CurrentReflex = _reflexPostConnect;
            }
            finally
            {
                _waitAck.Release();
            }
        }
    }
}
