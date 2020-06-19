using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace CK.MQTT.Common.Channels
{
    public class TcpChannelFactory : IMqttChannelFactory
    {
        public IMqttChannel Create( string connectionString )
        {
            string[] strs = connectionString.Split( ':' );
            return new TcpChannel( new TcpClient( strs[0], int.Parse( strs[1] ) ) );
        }
    }
}
