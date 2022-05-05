using System;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public interface IAuthenticationProtocolHandler : IAsyncDisposable
    {
        ValueTask<bool> ChallengeClientIdAsync( string clientId );
        ValueTask<bool> ChallengeShouldHaveCredsAsync( bool hasUserName, bool hasPassword );
        ValueTask<bool> ChallengeUserNameAsync( string userName );
        ValueTask<bool> ChallengePasswordAsync( string password );
    }
}
