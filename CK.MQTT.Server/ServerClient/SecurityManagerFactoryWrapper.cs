using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server.ServerClient
{
    class SecurityManagerFactoryWrapper : IAuthenticationProtocolHandlerFactory
    {
        readonly MqttServerClient _client;
        readonly IAuthenticationProtocolHandlerFactory _securityManagerFactory;

        public SecurityManagerFactoryWrapper( MqttServerClient client, IAuthenticationProtocolHandlerFactory securityManagerFactory )
        {
            _client = client;
            _securityManagerFactory = securityManagerFactory;
        }

        public async ValueTask<IAuthenticationProtocolHandler?> ChallengeIncomingConnectionAsync( string connectionInfo, CancellationToken cancellationToken )
        {
            if( _client._needClientTCS == null ) return null; //Deny all connection when we dont need a client.
            return await _securityManagerFactory.ChallengeIncomingConnectionAsync( connectionInfo, cancellationToken );
        }

        public void Dispose()
        {
            _securityManagerFactory.Dispose();
        }
    }
}
