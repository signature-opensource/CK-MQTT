using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests
{
    [ExcludeFromCodeCoverage]
    public class ConnectResultTests
    {
        [Test]
        public void ConnectResult_equals_works()
        {
            ConnectResult exception = new( ConnectError.InternalException );
            ConnectResult timeout = new( ConnectError.Timeout );

            ConnectResult connectedClean = new( SessionState.CleanSession, ConnectReturnCode.Accepted );
            ConnectResult serverRefused = new( SessionState.Unknown, ConnectReturnCode.ServerUnavailable );
            ConnectResult badPassword = new( SessionState.Unknown, ConnectReturnCode.BadUserNameOrPassword );
            ConnectResult sesionNotClean = new( SessionState.SessionPresent, ConnectReturnCode.Accepted );

            exception.Should().NotBe( new object() );
            Assert.That( exception != timeout );
            exception.Should().NotBe( connectedClean );

            serverRefused.Should().NotBe( badPassword );
            exception.Should().NotBe( serverRefused );
            connectedClean.Should().NotBe( sesionNotClean );

            Assert.That( connectedClean == new ConnectResult( SessionState.CleanSession, ConnectReturnCode.Accepted ) );
        }

        [Test]
        public void ConnectResult_GetHashCode()
        {
            ConnectResult exception = new( ConnectError.InternalException );
            ConnectResult timeout = new( ConnectError.Timeout );

            ConnectResult connectedClean = new( SessionState.CleanSession, ConnectReturnCode.Accepted );
            ConnectResult serverRefused = new( SessionState.Unknown, ConnectReturnCode.ServerUnavailable );
            ConnectResult badPassword = new( SessionState.Unknown, ConnectReturnCode.BadUserNameOrPassword );
            ConnectResult sesionNotClean = new( SessionState.SessionPresent, ConnectReturnCode.Accepted );

            exception.GetHashCode().Should().NotBe( timeout.GetHashCode() );
            exception.GetHashCode().Should().NotBe( connectedClean.GetHashCode() );

            serverRefused.GetHashCode().Should().NotBe( badPassword.GetHashCode() );
            exception.GetHashCode().Should().NotBe( serverRefused.GetHashCode() );
            connectedClean.GetHashCode().Should().NotBe( sesionNotClean.GetHashCode() );

            connectedClean.GetHashCode().Should().Be( new ConnectResult( SessionState.CleanSession, ConnectReturnCode.Accepted ).GetHashCode() );
        }
    }
}
