using System.Threading.Tasks;

namespace CK.MQTT.Server.Tests.Helpers;

class TestAuthHandler : IAuthenticationProtocolHandler
{
    public ValueTask<bool> ChallengeClientIdAsync( string clientId ) => new( true );

    public ValueTask<bool> ChallengePasswordAsync( string password ) => new( true );

    public ValueTask<bool> ChallengeShouldHaveCredsAsync( bool hasUserName, bool hasPassword ) => new( true );

    public ValueTask<bool> ChallengeUserNameAsync( string userName ) => new( true );

    public ValueTask DisposeAsync() => new();
}
