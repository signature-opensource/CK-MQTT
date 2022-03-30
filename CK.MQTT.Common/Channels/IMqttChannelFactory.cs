using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// A factory that produces <see cref="IMqttChannel"/>.
    /// </summary>
    public interface IMqttChannelFactory
    {
        /// <summary>
        /// This method will be called in the <see cref="ILowLevelMqtt3Client.ConnectAsync(IActivityMonitor, MqttClientCredentials?, OutgoingLastWill?)"/>.
        /// <br/> It must create a <see cref="IMqttChannel"/> connected to a broker.
        /// <br/> Exceptions will not be catched and throwed directly to the users, use it to signal to the user that you couldn't connect.
        /// </summary>
        /// <param name="m">The monitor to log activities of creating the channel.</param>
        /// <param name="connectionString">The connection string indicate where the channel must be connected.</param>
        /// <returns>A connected, ready to use, <see cref="IMqttChannel"/>.</returns>
        ValueTask<IMqttChannel> CreateAsync( string connectionString );
    }
}
