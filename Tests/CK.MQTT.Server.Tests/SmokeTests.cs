using CK.MQTT.Server.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace CK.MQTT.Server.Tests
{
    public class SmokeTests
    {
        [Test]
        public async Task server_and_client_connect_Async()
        {
            var server = new ServerTestHelper();
            await server.CreateClientAsync();
        }

        [Test]
        public async Task client_send_message_server_receive_it_Async()
        {
            var server = new ServerTestHelper();
            var (client, serverClient) = await server.CreateClientAsync();
            TaskCompletionSource tcs = new();
            serverClient.OnMessage.Simple.Sync += ( m, e ) =>
            {
                tcs.SetResult();
            };
            await client.PublishAsync( new SmallOutgoingApplicationMessage( "test", QualityOfService.AtMostOnce, false, Array.Empty<byte>() ) );
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            tcs.Task.IsCompleted.Should().BeTrue();
        }
    }
}
