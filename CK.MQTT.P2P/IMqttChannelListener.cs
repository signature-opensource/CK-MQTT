using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.P2P
{
    public interface IMqttChannelListener
    {
        Task<(IMqttChannel channel, string clientAddress)> AcceptIncomingConnection( CancellationToken cancellationToken );
    }
}
