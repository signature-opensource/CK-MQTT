using CK.Core;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using System;
using System.Net;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Processes
{
    static class ConnectProcess
    {
        public static async Task<ConnectResult> ExecuteConnectProtocol(
            IActivityMonitor m, IMqttChannel<IPacket> channel,
            string clientId, bool cleanSession, byte protocolLevel,
            string userName, string password, LastWill? will,
            ushort keepAliveSecs, int waitTimeoutSecs, string protocolName )
        {
            using( m.OpenInfo( "Executing connect protocol..." ) )
            {
                Connect connect = new Connect( clientId, cleanSession, keepAliveSecs, will, userName, password, protocolName );

                ConnectAck? ack = await await channel.SendAndWaitResponseAndLog<IPacket, ConnectAck>( m, connect, null, waitTimeoutSecs * 1000 );
                if( ack == null )
                {
                    throw new TimeoutException( $"While connecting, the server did not replied a CONNACK packet in {waitTimeoutSecs} secs." );
                }
                if( !(ack is ConnectAck connectAck) )
                {
                    throw new ProtocolViolationException( "While connecting, the server replied with a packet that was not a CONNACK." );
                }
                return new ConnectResult( connectAck.SessionState, connectAck.ConnectReturnCode );
            }
        }
    }
}
