using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    class SecurityManagerFactoryWrapper : ISecurityManagerFactory
    {
        readonly MqttServerClient _client;
        readonly ISecurityManagerFactory _securityManagerFactory;

        public SecurityManagerFactoryWrapper( MqttServerClient client, ISecurityManagerFactory securityManagerFactory )
        {
            _client = client;
            _securityManagerFactory = securityManagerFactory;
        }

        public async ValueTask<ISecurityManager?> ChallengeIncomingConnectionAsync( string connectionInfo, CancellationToken cancellationToken )
        {
            if( _client._needClientTCS == null ) return null; //Deny all connection when we dont need a client.
            return await _securityManagerFactory.ChallengeIncomingConnectionAsync( connectionInfo, cancellationToken );
        }
    }
}
