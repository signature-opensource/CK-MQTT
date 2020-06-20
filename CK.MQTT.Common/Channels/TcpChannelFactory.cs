using System.Net.Sockets;

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
