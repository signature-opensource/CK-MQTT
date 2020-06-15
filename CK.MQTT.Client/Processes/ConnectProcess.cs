using CK.Core;
using CK.MQTT.Client.Deserialization;
using CK.MQTT.Client.OutgoingPackets;
using CK.MQTT.Client.Reflexes;
using CK.MQTT.Common;
using CK.MQTT.Common.Channels;
using System;
using System.Buffers;
using System.Diagnostics;
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
            int waitTimeoutSecs,
            Reflex reflexPostConnect )
        {
            using( m.OpenInfo( "Executing connect protocol..." ) )
            {
                SemaphoreSlim waitAck = new SemaphoreSlim( 0 );
                ConnectResult? connect = default;
                var connackReflex = new ConnectAckReflex( input, reflexPostConnect, waitAck );
                input.CurrentReflex = connackReflex.ProcessIncomingPacket;
                output.QueueReflexMessage( outgoingConnect ); // This is sent as a reflex, there shouldn't be any message in both queue.
                bool waitRes = await waitAck.WaitAsync( waitTimeoutSecs * 1000 );
                if( connackReflex.Exception != null ) throw connackReflex.Exception;
                if( !waitRes ) return new ConnectResult( SessionState.Error, ConnectReturnCode.ServerUnavailable );
                if( !connect.HasValue ) throw new InvalidOperationException();
                return connect.Value;
            }
        }
    }
}
