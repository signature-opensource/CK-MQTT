using CK.MQTT.Server;

namespace CK.LogHub
{
    class TodoAuthHandlerFactory : IAuthenticationProtocolHandlerFactory
    {
        public ValueTask<IAuthenticationProtocolHandler?> ChallengeIncomingConnectionAsync( string connectionInfo, CancellationToken cancellationToken )
            => new ValueTask<IAuthenticationProtocolHandler?>( new TestAuthHandler() );
    }
}
