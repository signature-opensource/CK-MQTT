using CK.MQTT.Server;
using CK.MQTT.Server.Server;

var server = new MqttServer(
        new CK.MQTT.Mqtt3ConfigurationBase(),
        new TcpChannelFactory( 1883 ),
        new MemoryStoreFactory(),
        new TestAuthHandlerFactory()
    );

server.StartListening();

await Task.Delay( 500_000_000 );

await server.StopListeningAsync();

class TestAuthHandlerFactory : IAuthenticationProtocolHandlerFactory
{
    public ValueTask<IAuthenticationProtocolHandler?> ChallengeIncomingConnectionAsync( string connectionInfo, CancellationToken cancellationToken )
        => new( new TestAuthHandler() );
}

class TestAuthHandler : IAuthenticationProtocolHandler
{
    public ValueTask<bool> ChallengeClientIdAsync( string clientId ) => new( true );

    public ValueTask<bool> ChallengePasswordAsync( string password ) => new( true );

    public ValueTask<bool> ChallengeShouldHaveCredsAsync( bool hasUserName, bool hasPassword ) => new( true );

    public ValueTask<bool> ChallengeUserNameAsync( string userName ) => new( true );

    public ValueTask DisposeAsync() => new();
}


