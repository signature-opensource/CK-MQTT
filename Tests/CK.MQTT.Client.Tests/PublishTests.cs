using CK.MQTT.Client.Tests.Helpers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Tests
{
    public class PublishTests
    {
        [Test]
        public async Task simple_publish_qos0_works()
        {
            Scenario.ConnectedClient();
            //There is an issue with DisposableMessage:
            //It must be instantiated with a IDisposable. But it's the handler who need this IDisposable.
        }

        [Test]
        public async Task simple_publish_qos1_works()
        {
            throw new NotImplementedException();
        }

        [Test]
        public async Task simple_publish_qos2_works()
        {
            throw new NotImplementedException();
        }

        [Test]
        public async Task todo()
        {
            throw new NotImplementedException();
            // does the broker should respect the order ? (PUB A then PUB A, can it respond ACK B then ACK A ? )
            // test the case where a bad packet block a packet (cycling the ID don't reuse the blocked packet ID.)
            // if the response order should be respected, then we can resend packets immediatly after the response of a packet sent later.a
        }
    }
}
