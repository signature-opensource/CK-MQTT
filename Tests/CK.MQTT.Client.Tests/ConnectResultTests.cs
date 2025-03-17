using Shouldly;
using NUnit.Framework;

namespace CK.MQTT.Client.Tests;

public class ConnectResultTests
{
    [Test]
    public void ConnectResult_equals_works()
    {
        ConnectResult exception = new( ConnectError.InternalException );
        ConnectResult timeout = new( ConnectError.Timeout );

        ConnectResult connectedClean = new( SessionState.CleanSession, ProtocolConnectReturnCode.Accepted );
        ConnectResult serverRefused = new( SessionState.Unknown, ProtocolConnectReturnCode.ServerUnavailable );
        ConnectResult badPassword = new( SessionState.Unknown, ProtocolConnectReturnCode.BadUserNameOrPassword );
        ConnectResult sesionNotClean = new( SessionState.SessionPresent, ProtocolConnectReturnCode.Accepted );

        exception.ShouldNotBe( new object() );
        Assert.That( exception != timeout );
        exception.ShouldNotBe( connectedClean );

        serverRefused.ShouldNotBe( badPassword );
        exception.ShouldNotBe( serverRefused );
        connectedClean.ShouldNotBe( sesionNotClean );

        Assert.That( connectedClean == new ConnectResult( SessionState.CleanSession, ProtocolConnectReturnCode.Accepted ) );
    }

    [Test]
    public void ConnectResult_GetHashCode()
    {
        ConnectResult exception = new( ConnectError.InternalException );
        ConnectResult timeout = new( ConnectError.Timeout );

        ConnectResult connectedClean = new( SessionState.CleanSession, ProtocolConnectReturnCode.Accepted );
        ConnectResult serverRefused = new( SessionState.Unknown, ProtocolConnectReturnCode.ServerUnavailable );
        ConnectResult badPassword = new( SessionState.Unknown, ProtocolConnectReturnCode.BadUserNameOrPassword );
        ConnectResult sesionNotClean = new( SessionState.SessionPresent, ProtocolConnectReturnCode.Accepted );

        exception.GetHashCode().ShouldNotBe( timeout.GetHashCode() );
        exception.GetHashCode().ShouldNotBe( connectedClean.GetHashCode() );

        serverRefused.GetHashCode().ShouldNotBe( badPassword.GetHashCode() );
        exception.GetHashCode().ShouldNotBe( serverRefused.GetHashCode() );
        connectedClean.GetHashCode().ShouldNotBe( sesionNotClean.GetHashCode() );

        connectedClean.GetHashCode().ShouldBe( new ConnectResult( SessionState.CleanSession, ProtocolConnectReturnCode.Accepted ).GetHashCode() );
    }

}
