using CK.Core;
using CK.MQTT.Common.Channels;
using CK.MQTT.Common.Packets;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Client.Processes
{
    public static class DisconnectProcess
    {
        public static async ValueTask ExecuteDisconnectProtocol( IActivityMonitor m, IMqttChannel<IPacket> channel, int waitTimeoutMs )
        {
            CancellationTokenSource cancelSource = new CancellationTokenSource( waitTimeoutMs * 1000 );
            CancellationToken cancelToken = cancelSource.Token;
            await channel.SendAsync( m, new Disconnect(), cancelToken );
            await channel.CloseAsync( m, cancelToken );
        }
    }
}
