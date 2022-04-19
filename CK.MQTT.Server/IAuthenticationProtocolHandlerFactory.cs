using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public interface IAuthenticationProtocolHandlerFactory
    {
        /// <returns><see langword="null"/> when the incoming connection should be refused.</returns>
        ValueTask<IAuthenticationProtocolHandler?> ChallengeIncomingConnectionAsync( string connectionInfo, CancellationToken cancellationToken );
    }
}
