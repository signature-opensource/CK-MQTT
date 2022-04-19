using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// A factory that produces <see cref="IMqttChannel"/>.
    /// </summary>
    public interface IMqttChannelFactory : IDisposable
    {
        /// <summary>
        /// This method will be called in the <see cref="IConnectedMessageExchanger.ConnectAsync(IActivityMonitor, MqttClientCredentials?, OutgoingLastWill?)"/>.
        /// <br/> It must create a <see cref="IMqttChannel"/> connected to a broker.
        /// <br/> Exceptions will not be catched and throwed directly to the users, use it to signal to the user that you couldn't connect.
        /// </summary>
        /// <returns>A connected, ready to use, <see cref="IMqttChannel"/>.</returns>
        ValueTask<(IMqttChannel channel, string connectionInfo)> CreateAsync( CancellationToken cancellationToken );
    }
}
