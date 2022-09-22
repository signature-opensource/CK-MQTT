using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public interface IAuthenticationProtocolHandlerFactory : IDisposable
    {
        /// <returns><see langword="null"/> when the incoming connection should be refused.</returns>
        ValueTask<IAuthenticationProtocolHandler?> ChallengeIncomingConnectionAsync( string connectionInfo, CancellationToken cancellationToken );
    }

    class InsecureAuthHandlerFactory : IAuthenticationProtocolHandlerFactory
    {
        public ValueTask<IAuthenticationProtocolHandler?> ChallengeIncomingConnectionAsync( string connectionInfo, CancellationToken cancellationToken )
            => new( new InsecureAuthHandler() );

        public void Dispose()
        {
        }
    }
}
