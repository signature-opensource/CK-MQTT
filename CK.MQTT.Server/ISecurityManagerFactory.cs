using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public interface ISecurityManagerFactory
    {
        /// <returns><see langword="null"/> when the incoming connection should be refused.</returns>
        ValueTask<ISecurityManager?> ChallengeIncomingConnectionAsync( string connectionInfo, System.Threading.CancellationToken cancellationToken );
    }
}
