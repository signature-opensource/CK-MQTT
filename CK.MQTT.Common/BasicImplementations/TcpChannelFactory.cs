using System.Net.Sockets;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public class TcpChannelFactory : IMqttChannelFactory
    {
        public ValueTask<IMqttChannel> CreateAsync( IMqttLogger m, string connectionString )
        {
            string[] strs = connectionString.Split( ':' );
            return new ValueTask<IMqttChannel>( new TcpChannel( new TcpClient( strs[0], int.Parse( strs[1] ) ) ) );
        }
    }
}
