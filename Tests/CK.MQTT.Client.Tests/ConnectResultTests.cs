using FluentAssertions;
using NUnit.Framework;

namespace CK.MQTT.Client.Tests
{
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

            exception.Should().NotBe( new object() );
            Assert.That( exception != timeout );
            exception.Should().NotBe( connectedClean );

            serverRefused.Should().NotBe( badPassword );
            exception.Should().NotBe( serverRefused );
            connectedClean.Should().NotBe( sesionNotClean );

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

            exception.GetHashCode().Should().NotBe( timeout.GetHashCode() );
            exception.GetHashCode().Should().NotBe( connectedClean.GetHashCode() );

            serverRefused.GetHashCode().Should().NotBe( badPassword.GetHashCode() );
            exception.GetHashCode().Should().NotBe( serverRefused.GetHashCode() );
            connectedClean.GetHashCode().Should().NotBe( sesionNotClean.GetHashCode() );

            connectedClean.GetHashCode().Should().Be( new ConnectResult( SessionState.CleanSession, ProtocolConnectReturnCode.Accepted ).GetHashCode() );
        }

    }
}
