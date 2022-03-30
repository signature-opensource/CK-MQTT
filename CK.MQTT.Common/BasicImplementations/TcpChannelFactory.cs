using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Factory of <see cref="TcpChannel"/>.
    /// </summary>
    public class TcpChannelFactory : IMqttChannelFactory
    {
        /// <summary>
        /// Create a <see cref="TcpChannel"/>. The connection string should be "hostname:port".
        /// </summary>
        /// <param name="m">The logger to use.</param>
        /// <param name="connectionString">"hostname:port"</param>
        /// <returns></returns>
        public ValueTask<IMqttChannel> CreateAsync( string connectionString )
        {
            string[] strs = connectionString.Split( ':' );
            return new( new TcpChannel( strs[0], int.Parse( strs[1] ) ) );
        }
    }
}
