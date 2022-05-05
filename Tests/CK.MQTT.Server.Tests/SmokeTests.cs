using NUnit.Framework;
using System.Threading.Tasks;

namespace CK.MQTT.Server.Tests
{
    public class Tests
    {
        [Test]
        public async Task server_and_client_connect()
        {
            var server = new ServerTestHelper();
            (IConnectedMessageExchanger client, IConnectedMessageExchanger serverClient) = await server.CreateClient();
        }
    }
}
