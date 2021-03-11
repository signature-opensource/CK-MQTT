using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public interface IMqttChannelListener
    {
        Task<(IMqttChannel channel, string clientAddress)> AcceptIncomingConnection( CancellationToken cancellationToken );
    }
}
