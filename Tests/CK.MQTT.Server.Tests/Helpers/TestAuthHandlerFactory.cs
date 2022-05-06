using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server.Tests.Helpers
{
    class TestAuthHandlerFactory : IAuthenticationProtocolHandlerFactory
    {
        public ValueTask<IAuthenticationProtocolHandler?> ChallengeIncomingConnectionAsync( string connectionInfo, CancellationToken cancellationToken )
            => new( new TestAuthHandler() );
    }
}
