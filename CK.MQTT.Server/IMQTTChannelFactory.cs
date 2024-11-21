using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server;

/// <summary>
/// A factory that produces <see cref="IMQTTChannel"/>.
/// </summary>
public interface IMQTTChannelFactory : IDisposable
{
    /// <summary>
    /// This method will be called in the <see cref="IConnectedMessageSender.ConnectAsync(IActivityMonitor, MQTTClientCredentials?, OutgoingLastWill?)"/>.
    /// <br/> It must create a <see cref="IMQTTChannel"/> connected to a broker.
    /// <br/> Exceptions will not be catched and throwed directly to the users, use it to signal to the user that you couldn't connect.
    /// </summary>
    /// <returns>A connected, ready to use, <see cref="IMQTTChannel"/>.</returns>
    ValueTask<(IMQTTChannel channel, string connectionInfo)> CreateAsync( CancellationToken cancellationToken );
}
