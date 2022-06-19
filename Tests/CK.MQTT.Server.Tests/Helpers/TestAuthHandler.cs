using System.Threading.Tasks;

namespace CK.MQTT.Server.Tests.Helpers
{
    class TestAuthHandler : IAuthenticationProtocolHandler
    {
        public ValueTask<bool> ChallengeClientIdAsync( string clientId ) => new ValueTask<bool>( true );

        public ValueTask<bool> ChallengePasswordAsync( string password ) => new ValueTask<bool>( true );

        public ValueTask<bool> ChallengeShouldHaveCredsAsync( bool hasUserName, bool hasPassword ) => new ValueTask<bool>( true );

        public ValueTask<bool> ChallengeUserNameAsync( string userName ) => new ValueTask<bool>( true );

        public ValueTask DisposeAsync() => new ValueTask();
    }
}
